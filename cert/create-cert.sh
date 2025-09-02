#!/usr/bin/bash

# 1) Root CA (private) — 10 years
openssl genrsa -out rootCA.key 4096
openssl req -x509 -new -nodes -key rootCA.key -sha256 -days 3650 \
  -subj "/C=NO/ST=More og Romsdal/L=Kristiansund/O=Local Dev CA/CN=Ole Local Dev Root CA" \
  -out rootCA.crt

# 2) OpenSSL config with SANs for the leaf certificate
cat > site.openssl.cnf << 'EOF'
[ req ]
default_bits       = 2048
distinguished_name = req_distinguished_name
req_extensions     = v3_req
prompt             = no

[ req_distinguished_name ]
C  = NO
ST = More og Romsdal
L  = Kristiansund
O  = Dev
CN = greentechsee2025.paakobla.no

[ v3_req ]
keyUsage         = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName   = @alt_names

[ alt_names ]
DNS.1 = greentechsee2025.paakobla.no
DNS.2 = localhost
IP.1  = 127.0.0.1
IP.2  = ::1
EOF

# 3) Server key + CSR
openssl genrsa -out site.key 2048
openssl req -new -key site.key -out site.csr -config site.openssl.cnf

# 4) Sign CSR with your Root CA — 825 days is a safe default
openssl x509 -req -in site.csr -CA rootCA.crt -CAkey rootCA.key -CAcreateserial \
  -out site.crt -days 825 -sha256 -extfile site.openssl.cnf -extensions v3_req

# 5) Optional: quick check of the SANs
openssl x509 -in site.crt -noout -text | sed -n '/Subject Alternative Name/,+1p'

# 6) Export a PFX for Windows installation (you’ll be prompted for a password)
openssl pkcs12 -export -out site.pfx -inkey site.key -in site.crt -certfile rootCA.crt \
  -name "GreentechSee 2025 Dev Cert"
