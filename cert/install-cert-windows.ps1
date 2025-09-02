# Adjust to your paths
$RootPath = "rootCA.crt"
$PfxPath  = "site.pfx"

# 1) Install the Root CA into Local Machine Trusted Roots
Import-Certificate -FilePath $RootPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null

# 2) Install the site certificate (with private key) into Local Machine\Personal (My)
$pwd = Read-Host -AsSecureString "Enter the PFX password"
Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $pwd | Out-Null

Write-Host "Root CA installed to LocalMachine\Root and site cert installed to LocalMachine\My."
