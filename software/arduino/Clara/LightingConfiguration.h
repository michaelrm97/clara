#ifndef LIGHTING_CONFIGURATION_H_
#define LIGHTING_CONFIGURATION_H_

#include <Arduino.h>

#define HEADER_LEN      8
#define MAX_PAYLOAD_LEN 16384
#define MAX_CONTENT_LEN (HEADER_LEN + MAX_PAYLOAD_LEN)

typedef uint32_t Pattern;
typedef uint16_t Command;
typedef uint32_t Note;

#define PATTERN_LENGTH(pptr)  ((*pptr >> 24) & 0xFF)
#define PATTERN_REPEATS(pptr) ((*pptr >> 16) & 0xFF)
#define PATTERN_OFFSET(pptr)  ((*pptr >> 8) & 0xFF)
#define PATTERN_LED(pptr,i)   (pptr + i + 1)

#define COMMAND_IS_DELAY(c) ((c >> 15) & 0x1)
#define COMMAND_TYPE(c)     ((c >> 12) & 0x7)

#define COMMAND_DISPLAY_TYPE  0x0
#define COMMAND_SHIFT_TYPE    0x1
#define COMMAND_CLEAR_TYPE    0x2

#define COMMAND_DISPLAY_PATTERN(c) (c & 0xFFF)
#define COMMAND_SHIFT_ROTATE(c)    ((c >> 11) & 0x1)
#define COMMAND_SHIFT_SHIFT(c)     (c & 0xFF)
#define COMMAND_DELAY_DURATION(c)  (c & 0x7FFF)

#define NOTE_NOTE(n)     ((n >> 24) & 0xFF)
#define NOTE_VOLUME(n)   ((n >> 16) & 0xFF)
#define NOTE_DURATION(n) (n & 0xFFFF)

uint8_t *getData();
bool    checkIn(uint16_t len); // Check in data- ensure it is valid
void    resetState();

Pattern *getPattern(uint16_t offset);
Command *getNextCommand();
Note    *getNextNote();

#endif /* LIGHTING_CONFIGURATION_H_ */
