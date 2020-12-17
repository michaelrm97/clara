namespace LightingConfiguration

open System
open System.Text.RegularExpressions
open Microsoft.FSharp.Collections
open Notes

type PatternJsonObject (id: string, leds: string list, repeat: int, offset: int) =
    member __.id = id
    member __.leds = leds
    member __.repeat = repeat
    member __.offset = offset

type CommandJsonObject () = class end

type DisplayJsonObject (id: string) =
    inherit CommandJsonObject()
    member __.command = "display"
    member __.id = id

type ShiftJsonObject (rotate: bool, amount: int) =
    inherit CommandJsonObject()
    member __.command = "shift"
    member __.rotate = rotate
    member __.amount = amount
    
type ClearJsonObject () =
    inherit CommandJsonObject()
    member __.command = "clear"

type DelayJsonObject (duration: int) =
    inherit CommandJsonObject()
    member __.command = "wait"
    member __.duration = duration

type NoteJsonObject (note: string, volume: int, duration: int) =
    member __.note = note
    member __.volume = volume
    member __.duration = duration

type LightingConfigurationJsonObject (id: Guid, name: string, patterns: PatternJsonObject list, commands: CommandJsonObject list, music: NoteJsonObject list) =
    member __.id = id
    member __.name = name
    member __.patterns = patterns
    member __.commands = commands
    member __.music = music

type CommandInputObject (command: string, id: string, rotate: bool, amount: int, duration: int) =
    member __.command = command
    member __.id = id
    member __.rotate = rotate
    member __.amount = amount
    member __.duration = duration

type LightingConfigurationInputObject (name: string, patterns: PatternJsonObject list, commands: CommandInputObject list, music: NoteJsonObject list) =
    member __.name = name
    member __.patterns = patterns
    member __.commands = commands
    member __.music = music

[<AbstractClass>]
type Command () =
    abstract binary : int16
    abstract jsonObject : CommandJsonObject
    member __.length : int16 = (int16)1

type Color (value: int) =
    member __.Binary : int32 = value
    member __.JsonString : string = "#" + value.ToString("X6")

type Pattern (id: string, leds: Color list, repeat: int, offset: int) =
    member __.binary : int32 list =
        let header: int32 = ((List.length leds &&& 0xFF) <<< 24) ||| ((repeat &&& 0xFF) <<< 16) ||| ((offset &&& 0xFF) <<< 8)
        header :: (leds |> List.map (fun l -> l.Binary))
    member __.jsonObject : PatternJsonObject =
        PatternJsonObject(id, leds |> List.map (fun c -> c.JsonString), repeat, offset)
    member __.length : int16 = (int16)(1 + List.length leds)

type Display (id: string, patternOffset: int) =
    inherit Command()
    override __.binary = (int16)(patternOffset &&& 0xFFF)
    override __.jsonObject = DisplayJsonObject id :> _

type Shift (rotate: bool, amount: int) =
    inherit Command()
    override __.binary = (int16)((1 <<< 12) ||| ((Convert.ToInt32 rotate) <<< 11) ||| (amount &&& 0xFF))
    override __.jsonObject = ShiftJsonObject (rotate, amount) :> _

type Clear () =
    inherit Command()
    override __.binary = (int16)(2 <<< 12)
    override __.jsonObject = ClearJsonObject () :> _

type Delay (duration: int) =
    inherit Command()
    override __.binary = (int16)((1 <<< 15) ||| (duration &&& 0x7FFF))
    override __.jsonObject = DelayJsonObject duration :> _

type Note (note: NoteNum, volume: int, duration: int) =
    member __.binary: int32 =
        if note <> NoteNum.Rest then
            (int32)(((int note &&& 0xFF) <<< 24) ||| ((volume &&& 0xFF) <<< 16) ||| (duration &&& 0xFFFF))
        else (duration &&& 0xFFFF)
    member __.jsonObject: NoteJsonObject = NoteJsonObject (string note, volume, duration)
    member __.length : int16 = (int16)1

type LightingConfiguration (name: string, patterns: Pattern list, commands: Command list, notes: Note list) =
    member __.binary: byte list =
        let header = List.append [ byte 'C'; byte 'L' ] ([
                        patterns |> List.sumBy (fun p -> p.length)
                        commands |> List.sumBy (fun c -> c.length)
                        notes |> List.sumBy (fun n -> n.length)
                     ]
                     |> List.collect (BitConverter.GetBytes >> List.ofArray))
        let patternBytes = (patterns |> List.collect (fun p -> p.binary)) |> List.collect (BitConverter.GetBytes >> List.ofArray)
        let commandBytes = (commands |> List.map (fun c -> c.binary)) |> List.collect (BitConverter.GetBytes >> List.ofArray)
        let musicBytes = (notes |> List.map (fun n -> n.binary)) |> List.collect (BitConverter.GetBytes >> List.ofArray)
        let paddingBytes = if (List.length commands) % 2 = 1 then [(byte)0; (byte)0] else [];
        List.concat [header; patternBytes; commandBytes; paddingBytes; musicBytes]
    member __.jsonObject (id: Guid) : LightingConfigurationJsonObject =
        let patternsJsonObject = patterns |> List.map (fun p -> p.jsonObject)
        let commandsJsonObject = commands |> List.map (fun c -> c.jsonObject)
        let musicJsonObject = notes |> List.map (fun n -> n.jsonObject)
        LightingConfigurationJsonObject(id, name, patternsJsonObject, commandsJsonObject, musicJsonObject)

module LightingConfiguration =
    let MaxPayloadSize = 1 <<< 14

    let rec internal findError (a: Lazy<Result<unit, string>> list) : Result<unit, string> =
        match a with
        | [] -> Ok ()
        | x :: y ->
            match x.Force() with
            | Error s -> Error s
            | Ok () -> findError y

    let internal nameValid (name: string) : Result<unit, string> =
        if String.IsNullOrWhiteSpace name then
            Error "Name cannot be empty"
        else
        if String.length name > 255 then
            Error "Name cannot be longer than 255 characters"
        else Ok ()

    let internal patternValid (pattern: PatternJsonObject) : Result<unit, string> =
        if String.IsNullOrWhiteSpace pattern.id then
            Error "Pattern id cannot be empty"
        else
        if List.length pattern.leds < 1 || List.length pattern.leds > 60 then
            Error "Number of leds in pattern must be between 1 and 60"
        else
        if List.forall (fun led -> Regex.IsMatch(led, "^#[A-Fa-f0-9]{6}$")) pattern.leds |> not then
            Error "Each led in a pattern must be a valid HTML color string"
        else
        if pattern.repeat < 0 || pattern.repeat > 60 then
            Error "Repeat must be between 0 and 60 inclusive"
        else
        if pattern.offset < 0 || pattern.offset > 59 then
            Error "Offset must be between 0 and 59 inclusive"
        else Ok ()

    let internal patternsValid (patterns: PatternJsonObject list) : Result<unit, string> =
        match patterns |> List.map (fun p -> lazy(patternValid p)) |> findError with
        | Error s -> Error s
        | Ok () ->
            let patternIds = patterns |> List.map (fun p -> p.id)
            let distinctIds = List.distinct patternIds
            if List.length patternIds = List.length distinctIds then Ok() else Error "Pattern ids must be distinct"

    let internal commandValid (command: CommandInputObject) (patternIds: Set<string>) : Result<unit, string> =
        match command.command with
        | "display" ->
            if String.IsNullOrWhiteSpace command.id then
                Error "Pattern id in display command must not be empty"
            else
            if patternIds.Contains command.id |> not then
                Error "Pattern id in display command must match a specified pattern"
            else Ok ()
        | "shift" ->
            if command.amount < -59 || command.amount > 59 then
                Error "Shift amount must be between -59 and 59 inclusive"
            else Ok ()
        | "clear" -> Ok ()
        | "wait" ->
            if command.duration < 1 || command.duration > 32767 then
                Error "Wait duration must be between 1 and 32767 inclusive"
            else Ok ()
        | _ -> Error "Invalid command type"

    let internal commandsValid (patterns: PatternJsonObject list) (commands: CommandInputObject list) : Result<unit, string> =
        let patternIds = patterns |> List.map (fun p -> p.id) |> Set.ofList
        commands |> List.map (fun c -> lazy(commandValid c patternIds)) |> findError

    let internal noteValid (note: NoteJsonObject) : Result<unit, string> =
        let mutable noteValue = NoteNum.A4
        if Enum.TryParse<NoteNum>(note.note, &noteValue) |> not then
            Error "Note value is invalid"
        else
        if note.volume < 0 || note.volume > 128 then
            Error "Note volume must be between 0 and 128 inclusive"
        else
        if note.duration <1 || note.duration > 65535 then
            Error "Note duration must be between 1 and 65535 inclusive"
        else Ok ()

    let internal notesValid (notes: NoteJsonObject list) : Result<unit, string> =
        notes |> List.map (fun n -> lazy(noteValid n)) |> findError

    let internal convertPattern (pattern: PatternJsonObject) : Pattern =
        let leds = pattern.leds |> List.map ((fun s -> s.Substring 1) >> (fun s -> Convert.ToInt32 (s, 16)) >> (fun i -> Color(i)))
        Pattern(pattern.id, leds, pattern.repeat, pattern.offset)

    let internal convertCommand (patternOffsets: Map<string, int>) (command: CommandInputObject)  : Command =
        match command.command with
        | "display" -> Display(command.id, patternOffsets.[command.id]) :> Command
        | "shift" -> Shift(command.rotate, command.amount) :> Command
        | "clear" -> Clear() :> Command
        | "wait" -> Delay(command.duration) :> Command
        | _ -> failwith "Invalid command"

    let internal convertNote (note: NoteJsonObject) : Note =
        Note(Enum.Parse<NoteNum>(note.note), note.volume, note.duration)

    let internal isValid (json: LightingConfigurationInputObject) : Result<unit, string> =
        let isNameValid = lazy(nameValid json.name)
        let isPatternsValid = lazy(patternsValid json.patterns)
        let isCommandsValid = lazy(commandsValid json.patterns json.commands)
        let isNotesValid = lazy(notesValid json.music)
        [isNameValid; isPatternsValid; isCommandsValid; isNotesValid] |> findError

    let rec internal getPatternOffsets (patterns: PatternJsonObject list) (sum: int) : Map<string, int> =
        match patterns with
            | [] -> Map.empty<string, int>
            | x :: y ->
                let result = getPatternOffsets y (sum + 1 + List.length x.leds)
                result.Add (x.id, sum)

    let internal getPayloadSize (patterns: PatternJsonObject list) (commands: CommandInputObject list) (notes: NoteJsonObject list) =
        4 * (patterns |> List.sumBy (fun p -> 1 + List.length p.leds)) + 2 * (List.length commands) + 4 * (List.length notes)

    let convert (json: LightingConfigurationInputObject) : Result<LightingConfiguration, string> =
        match isValid json with
        | Error s -> Error s
        | Ok () ->
            let usedPatternIds = json.commands |> List.filter (fun c -> c.command = "display") |> List.map (fun c -> c.id) |> List.distinct |> Set.ofList
            let usedPatterns = json.patterns |> List.filter (fun p -> usedPatternIds.Contains p.id)

            // One last validation- ensure payload size is not too big
            if getPayloadSize usedPatterns json.commands json.music > MaxPayloadSize then
                Error "Lighting configuration is too large"
            else

            let patternOffsets = getPatternOffsets usedPatterns 0

            let name = json.name
            let patterns = usedPatterns |> List.map convertPattern
            let commands = json.commands |> List.map (convertCommand patternOffsets)
            let music = json.music |> List.map convertNote

            Ok (LightingConfiguration(name, patterns, commands, music))
