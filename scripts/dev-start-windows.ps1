param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,

    [ValidateSet('api', 'frontend', 'both')]
    [string]$Services = 'both'
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RootPath).Path
$logDirectory = Join-Path $root 'tmp\dev'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Start-PnmcApi {
    $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
    $apiDirectory = Join-Path $root 'pnmc-api'
    $project = Join-Path $apiDirectory 'src\PNMC.Api\PNMC.Api.csproj'
    $stdout = Join-Path $logDirectory 'api.stdout.log'
    $stderr = Join-Path $logDirectory 'api.stderr.log'

    $databaseName = if ($env:PNMC_LOCAL_DB_NAME) { $env:PNMC_LOCAL_DB_NAME } else { 'PNMC_LOCAL' }
    $databasePassword = $env:PNMC_LOCAL_SA_PASSWORD
    $databasePort = if ($env:PNMC_LOCAL_SQL_PORT) { $env:PNMC_LOCAL_SQL_PORT } else { '14344' }
    $apiPort = if ($env:PNMC_LOCAL_API_PORT) { $env:PNMC_LOCAL_API_PORT } else { '8180' }

    if ([string]::IsNullOrWhiteSpace($databasePassword)) {
        throw 'Falta PNMC_LOCAL_SA_PASSWORD. Definela en el entorno antes de iniciar la API.'
    }

    $env:ASPNETCORE_ENVIRONMENT = 'Local'
    # OBLIGATORIO CON watch Y VENTANA OCULTA. Un cambio que el reemplazo en caliente no sabe
    # aplicar -una ruta nueva, una firma distinta- es una "edicion brusca", y por omision
    # 'dotnet watch' se PARA a preguntar por consola si reinicia. Aqui el proceso arranca con
    # -WindowStyle Hidden y la salida redirigida: esa pregunta no la ve nadie y la API se queda
    # esperando una tecla que nunca llega.
    $env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = '1'
    $env:AZURE_SQL_CONNECTION_STRING = "Server=127.0.0.1,$databasePort;Initial Catalog=$databaseName;Persist Security Info=False;User ID=sa;Password=$databasePassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;"

    $quotedProject = '"' + $project + '"'

    # 'watch' ANTES DE 'run', y con --project delante: el vigilante necesita saber que proyecto
    # observar, y lo que va detras de 'run' se le pasa a la aplicacion.
    #
    # Antes era 'dotnet run' a secas, y el binario que atendia peticiones era el compilado al
    # arrancar: un cambio en el API quedaba invisible hasta pararlo y relanzarlo a mano.
    #
    # LOS ARGUMENTOS SE ARMAN AQUI Y NO EN LA LLAMADA porque un comentario dentro de una
    # continuacion con acento grave rompe el analisis del guion.
    $apiArguments = @(
        'watch',
        '--project', $quotedProject,
        'run',
        '--no-launch-profile',
        '--urls', "http://127.0.0.1:$apiPort;http://[::1]:$apiPort"
    )

    $process = Start-Process `
        -FilePath $dotnet `
        -ArgumentList $apiArguments `
        -WorkingDirectory $apiDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    Set-Content -LiteralPath (Join-Path $logDirectory 'api.pid') -Value $process.Id
}

function Start-PnmcFrontend {
    $npm = (Get-Command npm.cmd -ErrorAction Stop).Source
    $webDirectory = Join-Path $root 'pnmc-web'
    $stdout = Join-Path $logDirectory 'frontend.stdout.log'
    $stderr = Join-Path $logDirectory 'frontend.stderr.log'

    $process = Start-Process `
        -FilePath $npm `
        -ArgumentList @('start') `
        -WorkingDirectory $webDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    Set-Content -LiteralPath (Join-Path $logDirectory 'frontend.pid') -Value $process.Id
}

if ($Services -in @('api', 'both')) {
    Start-PnmcApi
}

if ($Services -in @('frontend', 'both')) {
    Start-PnmcFrontend
}
