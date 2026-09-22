// =====================================================================================
// PNMC — ambiente de pruebas en Azure
//
// QUÉ MONTA: un App Service que sirve el front y el API en el mismo origen, y una base
// Azure SQL. Nada más. Sin Blob Storage —el API no escribe ficheros— y sin Key Vault, que
// es una decisión aplazada y anotada, no un olvido.
//
// POR QUÉ ESTÁ ESCRITO Y NO SE HACE A CLIC: esta plantilla ES el manual de instalación que
// pide el Ministerio (DI-GSI-010 §3). Un ambiente montado a mano no se puede volver a
// levantar igual, ni entregar, ni revisar.
//
// CÓMO SE APLICA:
//   az group create --name rg-pnmc-pruebas --location brazilsouth
//   az deployment group create -g rg-pnmc-pruebas -f infra/main.bicep -p @infra/parametros.json
//
// ES IDEMPOTENTE: volver a aplicarla no rompe nada ni recrea la base. Eso es lo que la hace
// entregable, y es el primer punto de la verificación.
// =====================================================================================

targetScope = 'resourceGroup'

@description('Nombre del sitio. Da el dominio https://<nombre>.azurewebsites.net y tiene que ser único en todo Azure.')
@minLength(3)
@maxLength(40)
param nombreDelSitio string

@description('Región. brazilsouth es la más cercana a Colombia; eastus2 es más barata.')
param ubicacion string = resourceGroup().location

@description('Usuario administrador de la base. No puede llamarse «sa», «admin» ni «root»: Azure los rechaza.')
param usuarioSql string

@description('Contraseña del administrador de la base. No se escribe en ningún fichero del repositorio.')
@secure()
param claveSql string

@description('IP pública desde la que Divergente administra la base. Vacío = solo se entra desde el App Service.')
param ipDeAdministracion string = ''

@description('Etiquetas de inventario. El Ministerio pide registrar los componentes en su CMDB.')
param etiquetas object = {
  proyecto: 'PNMC'
  ambiente: 'pruebas'
  responsable: 'Divergente'
}

// -------------------------------------------------------------------------------------
// Plan de hospedaje
//
// B1 Linux: 1 núcleo, 1,75 GB, ~USD 13/mes. Es el escalón más bajo que permite
// «siempre encendido»; los gratuitos duermen y la primera visita del día tarda medio
// minuto, que en una demostración se lee como que el sitio no funciona.
// -------------------------------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${nombreDelSitio}'
  location: ubicacion
  tags: etiquetas
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true // «reserved» significa Linux. El nombre es de Azure, no mío.
  }
}

// -------------------------------------------------------------------------------------
// La aplicación
// -------------------------------------------------------------------------------------
resource sitio 'Microsoft.Web/sites@2023-12-01' = {
  name: nombreDelSitio
  location: ubicacion
  tags: etiquetas
  identity: {
    // Sin uso hoy —la base se abre con usuario y contraseña—, pero declararla ahora es lo
    // que permite pasar a identidad administrada sin recrear el sitio.
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'

      // UNA SOLA INSTANCIA, Y ESTÁ RAZONADO. `DatabaseBootstrapper` escribe en la base en
      // cada arranque —renombra claves del CMS, siembra las que faltan, poda historial— y no
      // toma ningún cerrojo. Con dos instancias eso corre dos veces contra la misma base.
      // Ponerle cerrojo es trabajo aparte; mientras tanto, el ambiente no escala y queda
      // escrito por qué.
      numberOfWorkers: 1

      // SIN `healthCheckPath`, Y NO ES UN OLVIDO. La sonda de App Service llama al contenedor
      // directamente por HTTP, sin `X-Forwarded-Proto`. Con `Security:ExigirHttps` activo eso
      // devuelve 307 y el sitio se declararía caído para siempre. El propio `Program.cs` avisa
      // de esto en su comentario. Con una sola instancia la sonda no aporta nada: el balanceo
      // que justifica su existencia no ocurre.

      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // El arranque ABORTA si esta lista está vacía fuera de local. Es el mismo origen que
          // sirve el sitio: no hay peticiones entre dominios, pero la política tiene que existir.
          name: 'Cors__AllowedOrigins__0'
          value: 'https://${nombreDelSitio}.azurewebsites.net'
        }
        {
          // El paquete se monta de solo lectura desde un ZIP. Publicar pasa a ser atómico:
          // o está la versión nueva entera o está la anterior entera.
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          // Sin esto, al reiniciar convive un momento la instancia vieja con la nueva, y las
          // dos ejecutan el arranque que escribe en la base. Ver `numberOfWorkers`.
          name: 'WEBSITE_DISABLE_OVERLAPPED_RECYCLING'
          value: '1'
        }
        {
          // Las claves que firman las tres cookies. `/home` es almacenamiento montado: sobrevive
          // al despliegue. Sin esto, cada publicación expulsa a todo el que estuviera dentro.
          name: 'Security__RutaDeClaves'
          value: '/home/pnmc-claves'
        }
      ]

      connectionStrings: [
        {
          // POR AQUÍ Y NO COMO AJUSTE DE APLICACIÓN: App Service la enmascara en el portal y la
          // publica como `ConnectionStrings:SqlServer`, que es la segunda fuente que consulta
          // `DatabaseConnectionResolver`. La primera —`AZURE_SQL_CONNECTION_STRING`— queda libre
          // para una excepción puntual sin tocar esto.
          name: 'SqlServer'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${servidorSql.properties.fullyQualifiedDomainName},1433;Initial Catalog=${baseDeDatos.name};User ID=${usuarioSql};Password=${claveSql};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
    }
  }
}

// -------------------------------------------------------------------------------------
// La base
// -------------------------------------------------------------------------------------
resource servidorSql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${nombreDelSitio}'
  location: ubicacion
  tags: etiquetas
  properties: {
    administratorLogin: usuarioSql
    administratorLoginPassword: claveSql
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource baseDeDatos 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: servidorSql
  name: 'pnmc-pruebas'
  location: ubicacion
  tags: etiquetas
  sku: {
    // Basic: 5 DTU, 2 GB, ~USD 5/mes. La base local mide 144 MB con 82 tablas, así que
    // sobra espacio de largo. Retiene 7 días de restauración a un punto en el tiempo.
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    // Español de Colombia, sin distinguir mayúsculas ni acentos: el mismo criterio con el que
    // se comparan los nombres en la base local.
    collation: 'Modern_Spanish_CI_AI'
    maxSizeBytes: 2147483648
  }
}

// El App Service sale por direcciones que cambian: esta regla es la forma que da Azure de
// decir «lo de dentro de Azure puede entrar». No abre la base a internet.
resource reglaServiciosDeAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: servidorSql
  name: 'PermitirServiciosDeAzure'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Para que el migrador se pueda ejecutar desde la máquina de Divergente cuando haga falta
// mirar algo a mano. Si el parámetro va vacío, esta regla no se crea.
resource reglaDeAdministracion 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (!empty(ipDeAdministracion)) {
  parent: servidorSql
  name: 'AdministracionDivergente'
  properties: {
    startIpAddress: ipDeAdministracion
    endIpAddress: ipDeAdministracion
  }
}

// -------------------------------------------------------------------------------------
// Lo que hace falta saber después de aplicar
// -------------------------------------------------------------------------------------
output direccionDelSitio string = 'https://${sitio.properties.defaultHostName}'
output servidorDeBaseDeDatos string = servidorSql.properties.fullyQualifiedDomainName
output nombreDeLaBase string = baseDeDatos.name
output identidadDelSitio string = sitio.identity.principalId
