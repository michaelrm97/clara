namespace Configs

open LightingConfiguration
open Notes

module SilentNight =
    let config : LightingConfiguration =
        LightingConfiguration(
            "Silent Night",
            [
                Pattern("test", [Color(0xFF0000)], 59, 0)
            ], [
                Display("test", 0); Delay(1500); Clear()
            ], [
                Note(NoteNum.A4, 128, 1000)
            ]
        )
