#include "Api.h"
#include "Lighting.h"
#include "LightingConfiguration.h"
#include "Notes.h"

#include "MyTone.h"

#define REFRESH_PERIOD 100 // Display every 100ms

#define POT_PIN A6

void setup() {
#ifdef SERIAL
  Serial.begin(9600);
#endif
  pinMode(POT_PIN, INPUT);
  
  setupApi();
  setupLights();

  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, HIGH);
}

bool isRunning = false;

bool commandsDone;
bool musicDone;

unsigned long nextCommandTime;
unsigned long nextMusicTime;
unsigned long nextRefreshTime;

void loop() {
  if (isRunning) {
    switch (checkCurrent()) {
      case NoChange:
        break;
      case NewConfig:
        // Stop everything
        stopConfig();
        isRunning = loadConfig();
        if (isRunning) {
          setupConfig();
        }
        return;
      case Stopped:
        // Stop everything
        isRunning = false;
        stopConfig();
        return;
    }

    auto currentTime = millis();
    auto commandProcessed = false;

    if (!commandsDone && currentTime >= nextCommandTime) {
      while (true) {
        auto command = getNextCommand();
        if (command) {
          if (COMMAND_IS_DELAY(*command)) {
            nextCommandTime += COMMAND_DELAY_DURATION(*command);
            break;
          } else {
            applyCommand(*command);
            commandProcessed = true;
          }
        } else {
          commandsDone = true;
          break;
        }
      }
    }

    if (!musicDone && currentTime >= nextMusicTime) {
      auto note = getNextNote();
      if (note) {
        playNote(*note);
        nextMusicTime += NOTE_DURATION(*note);
      } else {
        musicDone = true;
      }
    }

    if (commandProcessed || currentTime >= nextRefreshTime) {
      showLights(getBrightnessLevel());
      nextRefreshTime = currentTime + REFRESH_PERIOD;
    }

    if (commandsDone && musicDone) {
      resetLights();
      switch(nextConfig()) {
       case NoChange:
        // No need to load - just reset state
        resetState();
        setupConfig();
        break;
      case NewConfig:
        // Stop everything
        isRunning = loadConfig();
        if (isRunning) {
          setupConfig();
        }
        break;
      case Stopped:
        isRunning = false;
        return;
      }
    }
  } else {
    if (getConfig() == NewConfig) {
      isRunning = loadConfig();
      if (isRunning) {
        setupConfig();
      }
    }  
  }
}

void stopConfig() {
  resetLights();
  stopNote();
}

void setupConfig() {
  auto startTime = millis();

  nextCommandTime = startTime;
  nextMusicTime = startTime;
  nextRefreshTime = startTime;

  commandsDone = false;
  musicDone = false;
}

uint16_t getBrightnessLevel() {
  return analogRead(POT_PIN);
}
