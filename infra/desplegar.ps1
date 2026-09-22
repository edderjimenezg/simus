<#
    .SYNOPSIS
        Sube el estado actual del arbol de trabajo al ambiente de pruebas de Azure.

    .DESCRIPTION
        Reemplaza los seis pasos manuales del primer despliegue —construir el front, publicar el
        API, juntarlos, empaquetar, migrar y subir— por una sola orden que ademas comprueba las
        tres cosas que fallaron la primera vez y que no dan sintoma hasta que es tarde.

        NO EJECUTA LAS PRUEBAS. Las puertas descritas en CONTRIBUTING.md se corren antes de confirmar el
        cambio, no antes de subirlo; meterlas aqui haria que cada despliegue tardara cinco minutos
        de mas y que alguien acabara saltandoselas con un parametro.

    .PARAMETER Migrar
        Aplica los guiones de esquema pendientes antes de subir la aplicacion. SIN este parametro,
        el guion se DETIENE si hay pendientes en vez de subir igual.

        Es la unica proteccion real contra el modo de romper el ambiente que no da sintoma:
        publicar una aplicacion que espera columnas que la base de Azure todavia no tiene. El
        fallo no aparece al desplegar, aparece la primera vez que alguien abre la pantalla que usa
        esa columna, y para entonces ya nadie relaciona las dos cosas.

    .PARAMETER SoloAplicacion
        Salta la comprobacion del esquema por completo. Para cuando se sabe que el cambio es solo
        de front o de textos. Usarlo por costumbre anula el parrafo anterior.

    .EXAMPLE
        ./infra/desplegar.ps1
        ./infra/desplegar.ps1 -Migrar
#>

param(
    [switch]$Migrar,
    [switch]$SoloAplicacion,
    [string]$Sitio = "pnmc-pruebas",
    [string]$Grupo = "rg-pnmc-pruebas",
    [string]$Parametros = "infra/parametros.json"
)

$ErrorActionPreference = "Stop"
$raiz = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $raiz

function Paso([string]$texto) { Write-Output ""; Write-Output "== $texto" }

<#
    POR QUE EXISTE ESTE ENVOLTORIO, Y NO ES UN ADORNO.

    Con $ErrorActionPreference = "Stop", PowerShell convierte en error TERMINANTE cualquier linea
    que un ejecutable escriba en la salida de error, aunque el ejecutable termine con codigo 0.
    `npm run build` escribe sus avisos ahi —NG8113, dependencias que no son ESM— y eso basta para
    que el despliegue muera con un aviso de compilacion que no rompe nada. Medido: la primera
    ejecucion de este guion fallo por «LucideDatabase is not used within the template».

    La regla, entonces: los cmdlets siguen en Stop, que es lo que se quiere —un Get-Content sobre
    un fichero que no existe debe detenerlo todo—, y cada ejecutable se llama desde aqui, donde se
    juzga por su CODIGO DE SALIDA y no por lo que haya escrito.
#>
function Ejecutar([scriptblock]$bloque, [string]$queEs, [int[]]$codigosBuenos = @(0)) {
    $previo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $bloque
        if ($codigosBuenos -notcontains $LASTEXITCODE) {
            throw "$queEs fallo con codigo $LASTEXITCODE"
        }
    }
    finally { $ErrorActionPreference = $previo }
}

# Igual que el anterior, pero devuelve el codigo en vez de detenerse. Para `migrador estado`, que
# sale con 1 cuando hay pendientes y eso NO es un fallo: es la respuesta.
function CodigoDe([scriptblock]$bloque) {
    $previo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $bloque
        return $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previo }
}

# La CLI de Azure no suele estar en el PATH de una consola recien abierta.
$rutaAz = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"
if ((Test-Path $rutaAz) -and ($env:Path -notlike "*$rutaAz*")) {
    $env:Path = "$rutaAz;" + $env:Path
}

# ---------------------------------------------------------------------------------------------
# 1. El esquema primero. Si la base va detras, no se sube nada.
# ---------------------------------------------------------------------------------------------
if (-not $SoloAplicacion) {
    Paso "Esquema de la base"

    $p = Get-Content $Parametros -Raw | ConvertFrom-Json
    $servidor = "sql-$($p.parameters.nombreDelSitio.value).database.windows.net"
    $env:PNMC_MIGRADOR_CONEXION = "Server=tcp:$servidor,1433;Initial Catalog=$($p.parameters.nombreDelSitio.value);User ID=$($p.parameters.usuarioSql.value);Password=$($p.parameters.claveSql.value);Encrypt=True;Connection Timeout=60;"

    $codigo = CodigoDe { dotnet run --project pnmc-api/src/PNMC.Migrador -c Release --no-launch-profile -- estado | Out-Null }
    $pendientes = ($codigo -ne 0)

    if ($pendientes -and -not $Migrar) {
        throw "Hay guiones de esquema pendientes en la base de Azure. Vuelve a ejecutar con -Migrar, o con -SoloAplicacion si de verdad el cambio no toca la base."
    }

    if ($pendientes) {
        Ejecutar { dotnet run --project pnmc-api/src/PNMC.Migrador -c Release --no-launch-profile -- migrar } "el migrador"
        $despues = CodigoDe { dotnet run --project pnmc-api/src/PNMC.Migrador -c Release --no-launch-profile -- estado | Out-Null }
        if ($despues -ne 0) { throw "Despues de migrar sigue habiendo pendientes." }
        Write-Output "  esquema actualizado"
    }
    else {
        Write-Output "  al dia, no hay nada que aplicar"
    }
}

# ---------------------------------------------------------------------------------------------
# 2. Construir
# ---------------------------------------------------------------------------------------------
Paso "Construyendo el front"
Push-Location pnmc-web
try {
    Ejecutar { npm run build } "npm run build"
}
finally { Pop-Location }

Paso "Publicando el API"
# Ruta corta a proposito: la galeria tiene rutas como
# 'Galeria/Mesa Nacional de Rock - Instalacion/WhatsApp Image ....jpeg' que pasan el limite de
# 260 caracteres de Windows en cuanto la carpeta base es larga, y el empaquetado muere a medias.
$pub = "C:\Users\$env:USERNAME\AppData\Local\Temp\pnmc-publicacion"
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force -Confirm:$false }
Ejecutar { dotnet publish pnmc-api/src/PNMC.Api/PNMC.Api.csproj -c Release -o $pub --nologo | Out-Null } "dotnet publish"

Paso "Juntando el front dentro de wwwroot"
# robocopy usa los codigos 0..7 para exitos de distinto tipo: 1 es «se copiaron ficheros».
Ejecutar { robocopy "pnmc-web\dist\pnmc-web\browser" (Join-Path $pub "wwwroot") /E /NFL /NDL /NJH /NJS /NP | Out-Null } "robocopy" @(0, 1, 2, 3, 4, 5, 6, 7)
if (-not (Test-Path (Join-Path $pub "wwwroot\index.html"))) { throw "no hay wwwroot/index.html en el paquete" }

# ---------------------------------------------------------------------------------------------
# 3. Las dos comprobaciones que no dan sintoma
# ---------------------------------------------------------------------------------------------
Paso "Barrido de credenciales locales"
$fugas = Get-ChildItem $pub -Recurse -File | Where-Object { $_.Name -like "appsettings.Local*" }
if ($fugas) { throw "El paquete lleva $($fugas.Name -join ', '). No esta en git, pero si se copia a bin/." }
Write-Output "  sin appsettings.Local*"

Paso "Empaquetando"
# CreateFromDirectory de PowerShell 5.1 escribe los nombres con el separador del sistema
# ('wwwroot\index.html'). Un App Service Linux no lo convierte en carpeta: crea un fichero cuyo
# nombre contiene una barra invertida. La aplicacion arranca sin error, no encuentra
# wwwroot/index.html, y el sitio devuelve una pagina en blanco. Por eso las entradas se nombran
# a mano con '/'.
$zip = "C:\Users\$env:USERNAME\AppData\Local\Temp\pnmc-paquete.zip"
if (Test-Path $zip) { Remove-Item $zip -Force -Confirm:$false }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$flujo = [System.IO.File]::Open($zip, [System.IO.FileMode]::CreateNew)
$archivo = New-Object System.IO.Compression.ZipArchive($flujo, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in (Get-ChildItem $pub -Recurse -File)) {
        $nombre = $f.FullName.Substring($pub.Length + 1).Replace("\", "/")
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archivo, $f.FullName, $nombre)
    }
}
finally {
    $archivo.Dispose()
    $flujo.Dispose()
}

$lector = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $torcidas = @($lector.Entries | Where-Object { $_.FullName.Contains("\") }).Count
    $tieneIndice = @($lector.Entries | Where-Object { $_.FullName -eq "wwwroot/index.html" }).Count
    $total = $lector.Entries.Count
}
finally { $lector.Dispose() }

if ($torcidas -gt 0) { throw "$torcidas entradas del zip llevan barra invertida. En Linux no se descomprimen como carpetas." }
if ($tieneIndice -ne 1) { throw "el zip no tiene wwwroot/index.html" }
Write-Output ("  {0} entradas, 0 con barra invertida, {1:N1} MB" -f $total, ((Get-Item $zip).Length / 1MB))

# ---------------------------------------------------------------------------------------------
# 4. Subir y comprobar
# ---------------------------------------------------------------------------------------------
Paso "Subiendo a $Sitio"
Ejecutar { az webapp deploy --resource-group $Grupo --name $Sitio --src-path $zip --type zip --async false | Out-Null } "az webapp deploy"

Paso "Comprobacion de humo"
# El paquete tarda uno o dos minutos en montarse. Antes de eso los ficheros estaticos devuelven
# la pagina, que se lee como un defecto y no lo es. Por eso se reintenta en vez de fallar.
$base = "https://$Sitio.azurewebsites.net"
Add-Type -AssemblyName System.Net.Http
$cliente = New-Object System.Net.Http.HttpClient
$cliente.Timeout = [TimeSpan]::FromSeconds(120)

$ok = $false
foreach ($intento in 1..10) {
    Start-Sleep -Seconds 15
    try {
        $salud = $cliente.GetAsync("$base/health/ready").Result
        $icono = $cliente.GetAsync("$base/favicon.ico").Result
        $tipo = if ($icono.Content.Headers.ContentType) { $icono.Content.Headers.ContentType.MediaType } else { "" }
        if ([int]$salud.StatusCode -eq 200 -and [int]$icono.StatusCode -eq 200 -and $tipo -ne "text/html") {
            $ok = $true
            break
        }
        # ${} obligatorio: PowerShell lee «$intento:» como una variable con unidad y no compila.
        Write-Output "  intento ${intento}: todavia montandose"
    }
    catch { Write-Output "  intento ${intento}: sin respuesta" }
}

if (-not $ok) { throw "El sitio no respondio correctamente despues de 10 intentos. Mirar el log: az webapp log tail -g $Grupo -n $Sitio" }

$api = $cliente.GetAsync("$base/api/v1/ruta-que-no-existe").Result
if ([int]$api.StatusCode -ne 404) { throw "Una ruta inexistente del API devolvio $([int]$api.StatusCode) en vez de 404: el reparto al front esta tapando el API." }

Write-Output ""
Write-Output "Desplegado: $base"
