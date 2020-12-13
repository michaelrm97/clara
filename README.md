# Project C.L.A.R.A

## Christmas Light Array Running on Azure

This is an LED light display composed of light strips in the chape of a Christmas tree. The light strip is controlled by an Arduino MKR WiFi 1010 on a veroboard whose layout is described in `hardware/Clara.per`. This veroboard contains attached potentiometers and a speaker driven by an LM386. This communicates with a web server to retrieve lighting configurations, which also hosts a website for viewing and adjusting configurations.

This website is accessible at [https://project-clara.com](https://project-clara.com)

### Web Server

The web server is an ASP.NET core application written in F# using the SAFE stack, located in `software/web`. This exposes lighting configurations via a rest API along with a website for viewing and adjusting the configurations.

To run the web server locally run:

`dotnet fake build --target run`

To deploy the web server to Azure run:

`dotnet fake build --target Azure`

### Arduino Code

The Arduino code is located in `software/adruino/Clara`. It makes use of the WiFiNINA and ArduinoHttpClient libraries. Secrets such as the SSID and password are contained in `Secrets.h`, whilst the server name is configurable and contained in `Server.h`
