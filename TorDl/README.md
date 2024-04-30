# TorDl

Simple CLI to download files from the TOR network.

This tool uses HTTP client to download the files, but sets up a TOR proxy, so clearnet URLs will work as well, they will just be routed through the TOR network as well.

## Usage

```shell
tordl <url> [<options>]
```

*Example*

```shell
tordl https://duckduckgogg42xjoc72x3sjasowoarfbgcmvfimaftt6twagswzczad.onion/assets/logo_homepage.normal.v109.svg
```

## Options

### Set Request Timeout

Use `-t` or `--timeout` to set the number of seconds to use as request timeout.
You can set the value to 0 to disable request timeout.
The default value is 120 seconds.

*Example*

```shell
tordl https://example.com -t 3
```

This will set the request timeout to 3 seconds.