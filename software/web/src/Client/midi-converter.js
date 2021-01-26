const midi = require('midi-file');
const NOTE_NAMES = [
    "A",
    "AS",
    "B",
    "C",
    "CS",
    "D",
    "DS",
    "E",
    "F",
    "FS",
    "G",
    "GS"
];

const NOTE_ON = "noteOn";
const NOTE_OFF = "noteOff";

const DEFAULT_NEW_CONFIG_NAME = "New Lighting Config";
const DEFAULT_NEW_CONFIG_PATTERNS = [
    {
        "id": "green",
        "leds": [
          "#00FF00"
        ],
        "repeat": 0,
        "offset": 0
    }
];
const DEFAULT_NEW_CONFIG_COMMANDS = [
    {
        "command": "display",
        "id": "green"
    }
];
const ERROR_NEW_CONFIG_MUSIC = "Error parsing file - are you sure the file you provided is MIDI?";

function getClaraNoteNameFromMidiNumber(midiNumber) {
    return "" + NOTE_NAMES[((midiNumber - 21) % 12)] + (Math.floor((midiNumber - 24) / 12))
}

function convertMidiToJson(midiFile) {
    const midiFileBuffer = Buffer.from(midiFile);
    let jsonSong;
    try {
        jsonSong = midi.parseMidi(midiFileBuffer);
    } catch (e) {
        console.error(ERROR_NEW_CONFIG_MUSIC);
        alert(ERROR_NEW_CONFIG_MUSIC);
    }
    
    return jsonSong;
}

/**
 * Returns C.L.A.R.A JSON lighting config where the music property is generated 
 * from the given MIDI file represented as an ArrayBuffer. 
 * 
 * If a lighting config with invalid JSON is provided, the whole config is 
 * generated. Otherwise, the provided config music property will be modified.
 * 
 * If the provided file cannot be parsed, the returned config is NOT valid and 
 * states that the music property could not be generated.
 * 
 * NOTE: We only take the first track and only support playing one note at a time
 * 
 * Author: abtagle
 * 
 * @param {(ArrayBuffer, string)} input Input tuple containing and the current 
 *          input config we should modify if the config is valid JSON.
 */
export function convertMidiToClara(input) {
    // TODO: Figure how to make this work with Fable and accept two parameters 
    // instead of a tuple
    let config;
    try {
        config = JSON.parse(input[1]);
    } catch (e) {
        config = {
            "name": DEFAULT_NEW_CONFIG_NAME,
            "patterns": DEFAULT_NEW_CONFIG_PATTERNS,
            "commands": DEFAULT_NEW_CONFIG_COMMANDS,
        }
    }

    const jsonSong =  convertMidiToJson(input[0]);
    if (!jsonSong) {
        config.music = ERROR_NEW_CONFIG_MUSIC;
        return JSON.stringify(config, null, 2);
    }
    
    const currentNotesOn = {}
    const song = [];
    let currentTime = 0;
    let lastNoteOffTime;

    for (let i in jsonSong.tracks[0]) {
        let note = jsonSong.tracks[0][i];
        currentTime += note.deltaTime ? note.deltaTime : 0;
        if (note.type == NOTE_ON)
        {
            // Add rest if needed
            if(lastNoteOffTime && currentTime - lastNoteOffTime > 50) {
                song.push({
                    "note": "Rest",
                    "volume": 0,
                    "duration": currentTime - lastNoteOffTime
                })
            }

            currentNotesOn[note.noteNumber] = {
                "velocity": note.velocity,
                "noteTime": currentTime
            };
        } else if  (note.type == NOTE_OFF) {
            const onEventNote = currentNotesOn[note.noteNumber];
            song.push({
                "note": getClaraNoteNameFromMidiNumber(note.noteNumber),
                "volume": onEventNote.velocity,
                "duration": currentTime - onEventNote.noteTime
            });

            currentNotesOn[note.noteNumber] = null;
            lastNoteOffTime = currentTime;
        }
    }

    config.music = song;
    return JSON.stringify(config, null, 2);
}