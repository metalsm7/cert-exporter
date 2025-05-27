# Cert exporter for Prometheus

A Prometheus exporter that makes an HTTPS connection to the specified domain and exports the remaining time on the SSL certificate.

### Configuration

In the environment value “DOMAIN”, enter the domain you want to check.

```
# If you're only checking one
EXPORT DOMAIN="www.microsoft.com"

# Checking multiple
EXPORT DOMAIN="www.microsoft.com,www.google.com"
```

The default Listen port is 9972.

```
# If you want to change the listen port
EXPORT LISTEN=":9973"

# Change the listen address and port
EXPORT LISTEN="127.0.0.1:9972"
```

### Using Docker

```
$ docker run -e DOMAIN=www.microsoft.com,www.google.com -p 9972:9972 mparang/cert-exporter:latest
```