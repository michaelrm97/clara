#include "Api.h"
#include "Lighting.h"
#include "LightingConfiguration.h"
#include "Notes.h"

#define REFRESH_PERIOD 100 // Display every 100ms

#define POT_PIN A6

void setup() {
  // Serial.begin(9600);

  pinMode(POT_PIN, INPUT);
  
  setupApi();
  setupLights();
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
      // Serial.println("Commands done");
      resetLights();
      switch(nextConfig()) {
       case NoChange:
        // Serial.println("No change");
         // No need to load - just reset state
        resetState();
        setupConfig();
        break;
      case NewConfig:
        // Stop everything
        isRunning = loadConfig();
        if (isRunning) {
          // Serial.println("Setting up new config");  
          setupConfig();
        }
        break;
      case Stopped:
        // Serial.println("Stopped");
        isRunning = false;
        return;
      }
    }
  } else {
    if (getConfig() == NewConfig) {
      isRunning = loadConfig();
      if (isRunning) {
        // Serial.println("Setting up config");  
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
