#include "Api.h"

#include "LightingConfiguration.h"
#include "Secrets.h"
#include "Server.h"

#include <ArduinoHttpClient.h>
#include <WiFiNINA.h>

// Amount of time to wait between fetching configs
#define FETCH_CURRENT_CONFIG_PERIOD 1000

#define MAX_CURRENT_RESPONSE_LEN 128
#define MAX_PATH_LEN 128

const char* ssid = SECRET_SSID;
const char* pass = SECRET_PASS;

const char* serverAddress = SERVER_HOST;
const int port = 80;

const String lightingPath = "/api/lighting/";

WiFiClient wifi;
HttpClient client(wifi, serverAddress, port);

unsigned long nextCurrentConfigTime;

char *currentConfigId;
char *currentConfigTimestamp;

char response[2][MAX_CURRENT_RESPONSE_LEN + 1];
char path[MAX_PATH_LEN + 1];

char *currentResponse;
char *newResponse;

typedef enum apiStage {
  RequestNotSent,
  ReadingHeaders,
  ReadingBody
} ApiStage;

ApiStage apiStage;
int responseIndex;

static ApiStatus checkApiStatus() {
  // Check if None  
  if (!strcmp(newResponse, "None")) {
    return (currentConfigId == NULL) ? NoChange : Stopped;
  }

  // Parse newCurrentConfig
  char *newCurrentConfigId = newResponse;
  char *newCurrentConfigTimestamp = NULL;

  int i = 0;
  while (newResponse[i]) {
    if (newResponse[i] == ',') {
      newResponse[i] = '\0';
      newCurrentConfigTimestamp = newResponse + i + 1;
      break;
    }
    ++i;
  }

  if (newCurrentConfigTimestamp == NULL) {
    return NoChange;
  }

  // See if there is a change
  if (currentConfigId == NULL || strcmp(currentConfigId, newCurrentConfigId) || strcmp(currentConfigTimestamp, newCurrentConfigTimestamp)) {
    currentConfigId = newCurrentConfigId;
    currentConfigTimestamp = newCurrentConfigTimestamp;
    // Swap buffers
    char *tmp = currentResponse;
    currentResponse = newResponse;
    newResponse = tmp;
    return NewConfig;
  }
  return NoChange;
}

void setupApi() {
#ifdef SERIAL
  Serial.println("Attempting to connect");
#endif
  while (WiFi.begin(ssid, pass) != WL_CONNECTED);
#ifdef SERIAL
  Serial.println("Connected");
#endif
  nextCurrentConfigTime = millis();

  currentConfigId = NULL;
  currentConfigTimestamp = NULL;

  currentResponse = response[1];
  newResponse = response[0];

  // Pad with '\0'
  response[0][MAX_CURRENT_RESPONSE_LEN] = '\0';
  response[1][MAX_CURRENT_RESPONSE_LEN] = '\0';
  path[MAX_PATH_LEN] = '\0';

  apiStage = RequestNotSent;
}

// Get the current config (/api/current)
ApiStatus getConfig() {
  auto currentTime = millis();
  if (currentTime >= nextCurrentConfigTime) {
    nextCurrentConfigTime = currentTime + FETCH_CURRENT_CONFIG_PERIOD;
    client.beginRequest();
    client.get("/api/current");
    client.sendHeader("Accept", "text/plain");
    client.endRequest();

    int statusCode = client.responseStatusCode();
    if (statusCode != 200) {
      return NoChange;
    }
  
    if (client.skipResponseHeaders() != HTTP_SUCCESS) {
      return NoChange;
    }

    int len = client.contentLength();
    if (client.read((uint8_t *)newResponse, len) != len) {
      return NoChange;
    }
    newResponse[len] = '\0';

    return checkApiStatus();
  } else {
    return NoChange;
  }
}

// Move onto next config (/api/next)
ApiStatus nextConfig() {
  auto currentTime = millis();
  
  client.beginRequest();
  client.post("/api/next");
  client.sendHeader("Accept", "text/plain");
  client.sendHeader("Content-Length", "0");
  client.endRequest();

  int statusCode = client.responseStatusCode();
  if (statusCode != 200) {
    return NoChange;
  }

  if (client.skipResponseHeaders() != HTTP_SUCCESS) {
    return NoChange;
  }

  int len = client.contentLength();
  if (client.read((uint8_t *)newResponse, len) != len) {
    return NoChange;
  }
  newResponse[len] = '\0';

  return checkApiStatus();
}

// Check if there is a new config (non-blocking)
ApiStatus checkCurrent() {
  if (apiStage == RequestNotSent) {
    auto currentTime = millis();
    if (currentTime >= nextCurrentConfigTime) {
      nextCurrentConfigTime = currentTime + FETCH_CURRENT_CONFIG_PERIOD;
      client.beginRequest();
      client.get("/api/current");
      client.sendHeader("Accept", "text/plain");
      client.endRequest();
      apiStage = ReadingHeaders;
    } else {
      return NoChange;
    }
  }

  if (apiStage == ReadingHeaders) {
    while (!client.endOfHeadersReached()) {
      if (!client.readHeader()) {
        return NoChange;
      }
    }
    apiStage = ReadingBody;
    responseIndex = 0;
  }

  if (apiStage == ReadingBody) {
    while (!client.endOfBodyReached()) {
      int c = client.read();
      if (c < 0) {
        return NoChange;
      }
      newResponse[responseIndex++] = c;
    }
    newResponse[responseIndex] = '\0';
    apiStage = RequestNotSent;
    return checkApiStatus();
  }

  return NoChange;
}

// Load the currently set config
// Returns true if successful
bool loadConfig() {
  if (currentConfigId == NULL) {
    return false;
  }

  strncpy(path, "/api/lighting/", MAX_PATH_LEN);
  strncat(path, currentConfigId, MAX_PATH_LEN);
  
  client.beginRequest();
  client.get(path);
  client.sendHeader("Accept", "application/octet-stream");
  client.endRequest();

  int statusCode = client.responseStatusCode();
  if (statusCode != 200) {
    currentConfigId = NULL;
    currentConfigTimestamp = NULL;
    return false;
  }

  if (client.skipResponseHeaders() != HTTP_SUCCESS) {
    currentConfigId = NULL;
    currentConfigTimestamp = NULL;
    return false;
  }

  int len = client.contentLength();
  if (client.read(getData(), len) != len) {
    currentConfigId = NULL;
    currentConfigTimestamp = NULL;
    return false;
  }

  if (checkIn(len)) {
    return true;
  } else {
    currentConfigId = NULL;
    currentConfigTimestamp = NULL;
  }
}
