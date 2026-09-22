<#
    .SYNOPSIS
        Carga catalogos y datos de demostracion en una base de PNMC ya migrada.

    .DESCRIPTION
        El migrador aplica pnmc-database/schema/ y NO toca pnmc-database/seed/. Sin este paso la
        base queda con las 83 tablas vacias: el mapa no tiene municipios, los desplegables de
        departamento no tienen nada y no hay ningun contenido que mostrar.

        NO USA sqlcmd, Y NO ES UN CAPRICHO. El 26 de agosto de 2026 las tildes llegaron rotas a la
        base local porque sqlcmd de Windows leia el .sql por tuberia y lo interpretaba como CP850.
        Aqui el fichero se lee declarando UTF-8 y se manda por el cliente de SQL Server, que habla
        Unicode de punta a punta. Comprobado contra la base de Azure el 27 de agosto de 2026: 390
        de 1122 municipios llevan tilde o eñe y ninguno trae caracter de sustitucion.

        QUE SE CARGA Y QUE NO. Van las ocho semillas de catalogo y demostracion. Queda fuera
        V20260824_01__usuarios_roles_seed.sql, que asigna roles a las cinco cuentas de arranque
        —las de contraseña literal «admin»— y no tiene sentido en un sitio abierto a internet.
        Ninguna de las ocho crea cuentas: los correos que aparecen en ellas son datos de contacto
        de entidades, y no hay un solo hash de contraseña.

    .EXAMPLE
        $cadena = "Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=pnmc-pruebas;User ID=<usuario>;Password=<clave>;Encrypt=True;"
        ./infra/sembrar-datos.ps1 -Cadena $cadena -Carpeta (Resolve-Path pnmc-database/seed).Path

    .NOTES
        -Carpeta tiene que ser una ruta ABSOLUTA. Con una relativa, Test-Path la resuelve contra la
        ubicacion de PowerShell y [System.IO.File] contra la del proceso, que no siempre son la
        misma: el guion informa de exito habiendo leido cero ficheros.
#>

param(
    [Parameter(Mandatory = $true)][string]$Cadena,
    [Parameter(Mandatory = $true)][string]$Carpeta
)

$ErrorActionPreference = "Stop"

$orden = @(
    "V20260519_01__maestras_estaticas_seed.sql",
    "V20260519_02__divipola_seed.sql",
    "V20260519_03__administracion_control_seed.sql",
    "V20260519_04__contenidos_modulos_seed.sql",
    "V20260519_05__articulacion_lectura_comun_seed.sql",
    "V20260519_06__datos_prueba_amplios.sql",
    "V20260519_07__datos_moderacion_consola.sql",
    "V20260826_01__catalogo_editorial_seed.sql"
)

if (-not [System.IO.Path]::IsPathRooted($Carpeta)) {
    throw "-Carpeta tiene que ser una ruta absoluta. Recibido: $Carpeta"
}

$cn = New-Object System.Data.SqlClient.SqlConnection $Cadena
$cn.Open()

$fallos = 0
try {
    foreach ($nombre in $orden) {
        $ruta = Join-Path $Carpeta $nombre
        if (-not (Test-Path $ruta)) {
            Write-Output ("{0,-50} NO EXISTE" -f $nombre)
            $fallos++
            continue
        }

        $texto = [System.IO.File]::ReadAllText($ruta, [System.Text.Encoding]::UTF8)
        $lotes = [System.Text.RegularExpressions.Regex]::Split($texto, '(?im)^\s*GO\s*$') |
            Where-Object { $_.Trim().Length -gt 0 }

        $n = 0
        $error = $null
        foreach ($lote in $lotes) {
            $cmd = $cn.CreateCommand()
            $cmd.CommandText = $lote
            $cmd.CommandTimeout = 300
            try {
                $cmd.ExecuteNonQuery() | Out-Null
                $n++
            }
            catch {
                $error = $_.Exception.Message
                break
            }
        }

        if ($error) {
            Write-Output ("{0,-50} FALLO en el lote {1}: {2}" -f $nombre, ($n + 1), $error)
            $fallos++
        }
        else {
            Write-Output ("{0,-50} {1} lote(s)" -f $nombre, $n)
        }
    }
}
finally {
    $cn.Close()
}

if ($fallos -gt 0) {
    throw "$fallos semilla(s) no se aplicaron. La base queda incompleta."
}

Write-Output "Siembra completa."
