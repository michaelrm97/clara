#include "Lighting.h"

#include "LightingConfiguration.h"

#include <FastLED.h>

#define NUM_LEDS 59
#define BUFFER_SIZE 60
#define DATA_PIN 7

uint32_t ledBuffer[BUFFER_SIZE];
CRGB leds[NUM_LEDS];

static void fillPattern(Command command);
static void shiftBuffer(Command command);
static void clearBuffer();

void setupLights() {
  FastLED.addLeds<WS2812B, DATA_PIN, GRB>(leds, NUM_LEDS);
  clearBuffer();
}

void applyCommand(Command command) {
  switch (COMMAND_TYPE(command)) {
    case COMMAND_DISPLAY_TYPE:
      fillPattern(command);
      break;
    case COMMAND_SHIFT_TYPE:
      shiftBuffer(command);
      break;
    case COMMAND_CLEAR_TYPE:
      clearBuffer();
      break;
    default:
      break;
  }
}

void showLights(uint16_t brightness) {
  float factor = brightness / 1024.0;
  for (int i = 0; i < NUM_LEDS; i++) {
    uint8_t r = ((ledBuffer[i] >> 16) & 0xFF) * factor;
    uint8_t g = ((ledBuffer[i] >> 8) & 0xFF) * factor;
    uint8_t b = ((ledBuffer[i] >> 0) & 0xFF) * factor;
    leds[i] = CRGB(r, g, b);
  }
  FastLED.show();
}

void resetLights() {
  clearBuffer();
  showLights(0);
}

static void fillPattern(Command command) {
  Pattern *p = getPattern(COMMAND_DISPLAY_PATTERN(command));

  uint8_t len = PATTERN_LENGTH(p);
  uint8_t repeats = PATTERN_REPEATS(p);
  uint8_t offset = PATTERN_OFFSET(p);

  if (repeats == 0) {
    int i = 0;
    while (offset < BUFFER_SIZE) {
      ledBuffer[offset] = *(uint32_t*)PATTERN_LED(p,i);
      ++i;
      if (i == len) {
        i = 0;
      }
      ++offset;
    }
  } else {
    int i = 0;
    while (repeats > 0 && offset < BUFFER_SIZE) {
      ledBuffer[offset] = *(uint32_t*)PATTERN_LED(p,i);
      ++i;
      if (i == len) {
        i = 0;
        --repeats;
      }
      ++offset;
    }
  }  
}

static void shiftBuffer(Command command) {
  bool rotate = COMMAND_SHIFT_ROTATE(command);
  int8_t shift = COMMAND_SHIFT_SHIFT(command);
  while (shift < 0) {
    shift += BUFFER_SIZE;
  }
  if (rotate) {
    uint32_t tmp[BUFFER_SIZE];
    if (shift > 0) {
      memmove(tmp, &ledBuffer[BUFFER_SIZE - shift], shift * 4);
      memmove(&ledBuffer[shift], ledBuffer, (BUFFER_SIZE - shift) * 4);
      memmove(ledBuffer, tmp, shift * 4);
    }
  } else {
    if (shift > 0) {
      memmove(&ledBuffer[shift], ledBuffer, (BUFFER_SIZE - shift) * 4);
      memset(ledBuffer, 0, shift * 4);
    }
  }
}

static void clearBuffer() {
  memset(ledBuffer, 0, 4 * BUFFER_SIZE);
}
