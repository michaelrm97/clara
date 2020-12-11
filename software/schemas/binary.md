# Binary Format

Payload consists of a header followed by pattern definitions and a list of commands of lighting and music

| Section | Size (bytes) | Description |
| :- | :- | :- |
| Header | 8 | Contains header field and length of each section |
| Patterns | variable | Contains pattern definitions |
| Commands | variable | Contains lighting commands |
| Notes | variable | Contains notes |

## Header

Payload header consists of a header field (for sanity check), along with the size of each section.

| Section | Size (bytes) | Description |
| :- | :- | :- |
| Header field | 2 | The ASCII characters CL |
| Patterns section size | 2 | The size of the patterns section in bytes |
| Commands section size | 2 | The size of the commands section in bytes |
| Notes section size | 2 | The size of the notes section |

## Pattern

Each pattern consists of a 4 byte header, followed by an array of led colors, which are 4 bytes each. 

| Section | Size (bytes) | Description |
| :- | :- | :- |
| Header | 4 | Contains details about the pattern |
| Leds | No. Leds * 4 | Array of led colors |

### Pattern Header

Header contains the length of the led color array, as well as the number of times this pattern repeats and the offset from the start of the pattern. Total size is 4 bytes

| Section | Size (bits) | Description |
| :- | :- | :- |
| Length | 8 | Number of led colors in this pattern (1-59) |
| Repeats | 8 | Number of repeats (0-59) |
| Offset | 8 | Offset of pattern start (0-58) |
| Reserved | 8 | Reserved for future use |

### Led Color

24 bits of color data are contained in each 4 bytes word, which is padded to make construction of a CRGB simpler

| Section | Size (bits) | Description |
| :- | :- | :- |
| Reserved | 8 | Reserved for future use |
| Red | 8 | Red color brightness |
| Green | 8 | Green color brightness |
| Blue | 8 | Blue color brightness |

## Command

Each command can either be a delay or a regular command, as determined by the first bit. This allows for a delay of up to ~32s

| Section | Size (bits) | Description |
| :- | :- | :- |
| Delay | 1 | 1 if delay, 0 if regular command |
| Data | 15 | If delay, duration in ms (1-32767); command data otherwise |

### Regular Command

Each regular command contains a 3 bit field to indicate the command type, along with 12 bits for the command data

| Section | Size (bits) | Description |
| :- | :- | :- |
| Delay | 1 | 0 value for regular command |
| Type | 3 | Command type |
| Data | 12 | Command data |

### Display Commmand

| Section | Size (bits) | Description |
| :- | :- | :- |
| Delay | 1 | 0 value for regular command |
| Type | 3 | 0x0 |
| Pattern | 12 | Offset of pattern in array of patterns (in words) |

### Shift Command

| Section | Size (bits) | Description |
| :- | :- | :- |
| Delay | 1 | 0 value for regular command |
| Type | 3 | 0x1 |
| Rotate | 1 | Whether data should rotate from one end to the other |
| Reserved | 3 | Reserved for future use |
| Shift | 8 | Amount to shift by (signed) (-29-30) |

### Clear Command

| Section | Size (bits) | Description |
| :- | :- | :- |
| Delay | 1 | 0 value for regular command |
| Type | 3 | 0x2 |
| Reserved | 12 | Reserved for future use |

## Note

Each note consists of a MIDI note number, volume (amplitude of sine wave) and duration (up to ~65s)

| Section | Size (bits) | Description |
| :- | :- | :- |
| Note | 8 | MIDI note number |
| Volume | 8 | Volume (0-128) |
| Duration | 16 | Duration in ms (1-65535) |
