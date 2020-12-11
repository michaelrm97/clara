#include "LightingConfiguration.h"

typedef struct configHeader {
  char magic1;
  char magic2;
  uint16_t lenPatterns;
  uint16_t lenCommands;
  uint16_t lenNotes;
} ConfigHeader;

uint8_t data[MAX_CONTENT_LEN];

ConfigHeader *header = (ConfigHeader *)data;

uint16_t numCommands;
uint16_t numNotes;

uint16_t currentCommand;
uint16_t currentNote;

Pattern *patterns;
Command *commands;
Note *notes;

uint8_t *getData() {
  return data;
}

bool checkIn(uint16_t len) {
  // Check magic numbers
  if (header->magic1 != 'C' || header->magic2 != 'L') {
    return false;
  }

  // Check lengths match
  if (header->lenPatterns + header->lenCommands + header->lenNotes != len) {
    return false;
  }

  // Reset state
  numCommands = header->lenCommands / 2;
  numNotes = header->lenNotes / 4;

  currentCommand = 0;
  currentNote = 0;

  patterns = (Pattern *)(data + HEADER_LEN);
  commands = (Command *)(data + HEADER_LEN + header->lenPatterns);
  notes = (Note *)(data + HEADER_LEN + header->lenPatterns + header->lenCommands);

  return true;
}

void resetState() {
  currentCommand = 0;
  currentNote = 0;
}

Pattern *getPattern(uint16_t offset) {
  return patterns + offset;
}
Command *getNextCommand() {
  if (currentCommand == numCommands) {
    return NULL;  
  }

  return commands + currentCommand++;
}

Note *getNextNote() {
  if (currentNote == numNotes) {
    return NULL;
  }

  return notes + currentNote++;
}
