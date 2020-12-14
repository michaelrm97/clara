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

  auto patternSectionSize = 4 * header->lenPatterns;
  auto commandSectionSize = 2 * header->lenCommands;
  auto notesSectionSize = 4 * header->lenNotes;

  while (commandSectionSize % 4) {
    commandSectionSize++;
  }

  // Check lengths match
  if (patternSectionSize + commandSectionSize + notesSectionSize + HEADER_LEN != len) {
    return false;
  }

  // Reset state
  numCommands = header->lenCommands;
  numNotes = header->lenNotes;

  currentCommand = 0;
  currentNote = 0;

  patterns = (Pattern *)(data + HEADER_LEN);
  commands = (Command *)(data + HEADER_LEN + patternSectionSize);
  notes = (Note *)(data + HEADER_LEN + patternSectionSize + commandSectionSize);

  for (int i = 0; i < numCommands; i++) {
    if (COMMAND_IS_DELAY(commands[i])) {
    } else {
      switch (COMMAND_TYPE(commands[i])) {
        case COMMAND_DISPLAY_TYPE:
          break;
        case COMMAND_SHIFT_TYPE:
          break;
        case COMMAND_CLEAR_TYPE:
          break;
      }
    }
  }

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
