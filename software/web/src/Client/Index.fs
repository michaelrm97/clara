module Index

open System
open Elmish
open Fetch

open Shared
open Fable.Core
open Fable.SimpleJson
open Fable.Core.JsInterop

type LightingConfigurationUI =
    { id : Guid
      name : string
      formatted : string }

type Model =
    { Configs: LightingConfigurationUI list
      Current: Guid Option
      Selected: Guid Option
      MidiFile: Browser.Types.Blob
      Input: string
      Error: string }

type Msg =
    | GotConfigs of LightingConfigurationUI list * Guid Option
    | AddedConfig of Result<LightingConfigurationUI, string>
    | UpdatedConfig of Result<LightingConfigurationUI, string>
    | CurrentConfig of Guid Option
    | DeletedConfig of Guid
    | SetInput of string
    | SelectConfig of Guid
    | StoppedConfig
    | NewConfig
    | AddConfig
    | UpdateConfig
    | DeleteConfig
    | PlayConfig
    | StopConfig
    | NextConfig
    | Refresh
    | LoadMidiFile
    | SaveMidiFile of Browser.Types.Blob
    | ErrorReadingFile

module ConfigJson =
    type IConfigJson =
        abstract parseConfigList : string -> LightingConfigurationUI[]
        abstract parseConfig : string -> LightingConfigurationUI
        abstract getConfig : string -> JS.Promise<Response>
        abstract postConfig : string -> string -> JS.Promise<Response>
        abstract putConfig : string -> string -> JS.Promise<Response>
        abstract deleteConfig : string -> JS.Promise<Response>

    [<Import("*", "./public/app.js")>]
    let lib: IConfigJson = jsNative

module LightingApi =
    let getConfigsAndCurrent = fun () ->
        let fetchConfigs =
            ConfigJson.lib.getConfig "api/lighting"
                |> Promise.bind (fun res -> res.text())
                |> Promise.map (fun raw -> ConfigJson.lib.parseConfigList raw |> List.ofArray)
                    
        let fetchCurrent =
            ConfigJson.lib.getConfig "api/current"
                |> Promise.bind (fun res -> res.json<CurrentConfigResponse> ())
                |> Promise.map (fun res ->
                    match res.id with
                    | null -> None
                    | id -> Some (Guid.Parse id))

        fetchConfigs
            |> Promise.bind (fun configs -> Promise.map (fun current -> configs, current) fetchCurrent)
    let addConfig = fun (input: string) ->
        ConfigJson.lib.postConfig "api/lighting" input
            |> Promise.bind (fun res ->
                if res.Status = 201 then
                    res.text() |> Promise.map (fun r -> Ok (ConfigJson.lib.parseConfig r))
                else
                if res.Status < 500 then
                    res.json<ErrorResponse>() |> Promise.map (fun r -> Error r.message)
                else Error "Invalid lighting configuration" |> Promise.lift)
            |> Promise.catch (fun _ -> Error "Error adding configuration")

    let updateConfig = fun (id: Guid, input: string) ->
        let url = sprintf "api/lighting/%s" (id.ToString())
        ConfigJson.lib.postConfig url input
        |> Promise.bind (fun res ->
            printfn "%i" res.Status
            if res.Status = 200 then
                res.text() |> Promise.map (fun r -> Ok (ConfigJson.lib.parseConfig r))
            else
            if res.Status < 500 then
                res.json<ErrorResponse>() |> Promise.map (fun r -> Error r.message)
            else Error "Invalid lighting configuration" |> Promise.lift)
        |> Promise.catch (fun _ -> Error "Error updating configuration")

    let deleteConfig = fun (id: Guid) ->
        let url = sprintf "api/lighting/%s" (id.ToString())
        ConfigJson.lib.deleteConfig url
            |> Promise.map (fun _ -> ())

    let playConfig = fun (id: Guid) ->
        let input = {
            id = id.ToString()
        }
        ConfigJson.lib.postConfig "api/next" (Json.stringify input)
            |> Promise.bind (fun res ->
                if res.Status = 200 then
                    res.json<CurrentConfigResponse> ()
                    |> Promise.map (fun res ->
                        match res.id with
                        | null -> None
                        | id -> Some (Guid.Parse id))
                else None |> Promise.lift)
            |> Promise.catch (fun _ -> None)

    let stopConfig = fun () ->
        ConfigJson.lib.postConfig "api/stop" ""
            |> Promise.map (fun _ -> ())

    let nextConfig = fun () ->
        ConfigJson.lib.postConfig "api/next?start" ""
            |> Promise.bind (fun res ->
                if res.Status = 200 then
                    res.json<CurrentConfigResponse> ()
                    |> Promise.map (fun res ->
                        match res.id with
                        | null -> None
                        | id -> Some (Guid.Parse id))
                else None |> Promise.lift)
            |> Promise.catch (fun _ -> None)

let findById x = fun c -> c.id = x

let init(): Model * Cmd<Msg> =
    let model =
        { Configs = []
          Current = None
          Selected = None
          MidiFile = downcast new System.Object()
          Input = ""
          Error = "" }
    let cmd = Cmd.OfPromise.perform LightingApi.getConfigsAndCurrent () GotConfigs
    model, cmd

let convertMidiToClara : Browser.Types.Blob * string -> string = import "convertMidiToClara" "./midi-converter.js" 

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | GotConfigs (configs, current) ->
        let newSelected =
            match model.Selected with
            | None ->
                if String.IsNullOrWhiteSpace model.Input then
                    match configs with
                        | [] -> None
                        | x -> Some (List.head configs).id
                else None
            | Some x -> if List.exists (findById x) configs then Some x else None
        let newInput =
            if newSelected <> model.Selected then
                match newSelected with
                | Some x -> (List.find (findById x) configs).formatted
                | None -> ""
            else model.Input
        { model with Configs = configs; Current = current; Selected = newSelected; Input = newInput; Error = "" }, Cmd.none
    | AddedConfig added ->
        let newModel =
            match added with
            | Ok a ->
                { model with Configs = List.append model.Configs [a]; Selected = Some (a.id); Input = a.formatted; Error = "" }
            | Error e ->
                { model with Error = e }
        newModel, Cmd.none
    | UpdatedConfig updated ->
        let newModel =
            match updated with
            | Ok u ->
                { model with Configs = List.map (fun i -> if i.id = u.id then u else i) model.Configs; Selected = Some (u.id); Input = u.formatted; Error = "" }
            | Error e ->
                { model with Error = e }
        newModel, Cmd.none
    | CurrentConfig _current ->
        model, Cmd.ofMsg Refresh
    | DeletedConfig id ->
        let newConfigs = List.filter (fun c -> c.id <> id) model.Configs
        let newSelected =
            match newConfigs with
                | [] -> None
                | x -> Some (List.head newConfigs).id
        let newInput =
            match newSelected with
            | Some x -> (List.find (findById x) newConfigs).formatted
            | None -> ""
        let newCurrent = if model.Current = Some id then None else model.Current
        { model with Configs = newConfigs; Selected = newSelected; Input = newInput; Current = newCurrent; Error = "" }, Cmd.none
    | StoppedConfig ->
        { model with Current = None; Error = "" }, Cmd.none
    | SetInput input ->
        { model with Input = input }, Cmd.none
    | SelectConfig selected ->
        { model with Selected = Some selected; Input = (List.find (findById selected) model.Configs).formatted; Error = "" }, Cmd.none
    | NewConfig ->
        { model with Selected = None; Input = ""; Error = "" }, Cmd.none
    | AddConfig ->
        model, Cmd.OfPromise.perform LightingApi.addConfig model.Input AddedConfig
    | UpdateConfig ->
        match model.Selected with
        | None -> model, Cmd.none
        | Some x -> model, Cmd.OfPromise.perform LightingApi.updateConfig (x, model.Input) UpdatedConfig
    | DeleteConfig ->
        match model.Selected with
        | None -> model, Cmd.none
        | Some x -> model, Cmd.OfPromise.perform LightingApi.deleteConfig x (fun _ -> DeletedConfig x)
    | PlayConfig ->
        match model.Selected with
        | None -> model, Cmd.none
        | Some x -> model, Cmd.OfPromise.perform LightingApi.playConfig x CurrentConfig
    | StopConfig ->
        model, Cmd.OfPromise.perform LightingApi.stopConfig () (fun _ -> StoppedConfig)
    | NextConfig ->
        model, Cmd.OfPromise.perform LightingApi.nextConfig () CurrentConfig
    | Refresh ->
        model, Cmd.OfPromise.perform LightingApi.getConfigsAndCurrent () GotConfigs
    | LoadMidiFile ->
        let newInput = convertMidiToClara (model.MidiFile, model.Input)
        { model with Input = newInput }, Cmd.none
    | SaveMidiFile midiFile ->
        { model with MidiFile = midiFile }, Cmd.none

open Fable.React
open Fable.React.Props
open Fulma
open Fable.FontAwesome

let containerBox (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ] [
        // Current config
        Level.level [ ] [
            Level.left [ ] [
                Level.item [ ] [
                    strong [ ] [ str "Current lighting configuration:" ]
                ]
            ]
            Level.right [ ] [
                Level.item [ ] [
                    match model.Current with
                    | None -> str "None"
                    | Some x ->
                        let name = (List.find (findById x) model.Configs).name
                        Content.content [
                            Content.Modifiers [
                                Modifier.TextColor IsLink
                            ]
                        ] [
                            a [ OnClick (fun _ -> SelectConfig x |> dispatch) ] [ str name ]
                        ]
                ]
            ]
        ]

        Level.level [ ] [
            Level.left [ ] [
                Level.item [ ] [
                    strong [ ] [ str "Select:" ]
                ]
            ]
            Level.right [ ] [
                Level.item [ ] [
                    let value, isActive, isNone =
                        match model.Selected with
                        | None ->
                            "New", (fun _ -> false), true
                        | Some x ->
                            (List.find (findById x) model.Configs).name, (fun (config: LightingConfigurationUI) -> config.id = x), false

                    Dropdown.dropdown [
                        Dropdown.IsHoverable
                        Dropdown.IsRight
                    ] [
                        Dropdown.trigger [ ] [
                            Button.button [ ] [
                                span [ ] [
                                    Content.content
                                        (if isNone then [ Content.Modifiers [ Modifier.TextTransform TextTransform.Italic ] ] else [])
                                        [ str value ]
                                ]
                                Icon.icon [ Icon.Size IsSmall ] [
                                    Fa.i [ Fa.Solid.AngleDown ] [ ]
                                ]
                            ]
                        ]
                        Dropdown.menu [ ] [
                            let options = [
                                for config in model.Configs do
                                    Dropdown.Item.a [
                                        Dropdown.Item.IsActive (isActive config)
                                        Dropdown.Item.Props [ OnClick (fun _ -> SelectConfig config.id |> dispatch) ]
                                    ] [ str config.name ]
                            ]
                            Dropdown.content [ ]
                                (if isNone |> not then
                                    List.append options [
                                        Dropdown.divider [ ] 
                                        Dropdown.Item.a [
                                            Dropdown.Item.Props [ OnClick (fun _ -> NewConfig |> dispatch) ]
                                        ] [ str "New" ]
                                    ] else options)
                        ]
                    ]
                ]
            ]
        ]

        Level.level [ ] [
            Level.left [ ] [
                Level.item [ ] [
                    match model.Selected with
                    | Some x ->
                        Button.button [
                            Button.Color IsInfo
                            Button.OnClick (fun _ -> UpdateConfig |> dispatch)
                            Button.Disabled ((model.Configs |> List.find (findById x)).formatted = model.Input)
                        ] [
                            str "Update"
                        ]
                    | None ->
                        Button.button [
                            Button.Color IsInfo
                            Button.OnClick (fun _ -> AddConfig |> dispatch)
                            Button.Disabled (String.IsNullOrWhiteSpace model.Input)
                        ] [
                            str "Add"
                        ]
                ]
            ]
            Level.right [ ] [
                Level.item [ ] [
                    Button.button [
                        Button.Color IsSuccess
                        Button.OnClick (fun _ -> PlayConfig |> dispatch)
                        Button.Disabled (model.Selected = None)
                    ] [ str "Play" ]
                ]
                Level.item [ ] [
                    Button.button [
                        Button.Color IsPrimary
                        Button.OnClick (fun _ -> NextConfig |> dispatch)
                        Button.Disabled (List.length model.Configs = 0)
                    ] [ str "Next" ]
                ]
                Level.item [ ] [
                    Button.button [
                        Button.Color IsWarning
                        Button.OnClick (fun _ -> StopConfig |> dispatch)
                        Button.Disabled (model.Selected = None)
                    ] [ str "Stop" ]
                ]
                Level.item [ ] [
                    Button.button [
                        Button.Color IsDanger
                        Button.OnClick (fun _ -> DeleteConfig |> dispatch)
                        Button.Disabled (model.Selected = None)
                    ] [ str "Delete" ]
                ]
            ]
        ]

        Level.level [ ] [
            Textarea.textarea [
                Textarea.Value model.Input
                Textarea.OnChange (fun x -> SetInput x.Value |> dispatch)
            ] []
        ]
        
        label [ ] [
            str "Upload a MIDI file to generate music for the current config."
        ]
        input [ 
            Class "input"
            Type "file"
            OnInput (fun ev -> 
                let file = ev.target?files?(0)
                let reader = Browser.Dom.FileReader.Create()

                try 
                    reader?readAsArrayBuffer(file)
                with
                    | Failure msg -> System.Console.WriteLine("No file selected. " + msg)

                reader.onload <- fun evt ->
                    dispatch (SaveMidiFile evt.target?result)

                    try
                        dispatch LoadMidiFile
                    with 
                        | Failure msg -> System.Console.WriteLine("Error parsing file as MIDI. " + msg)

                reader.onerror <- fun evt ->
                    System.Console.WriteLine("Error parsing file.")
            ) 
        ]


        Text.p [
            Modifiers [
                Modifier.TextColor IsDanger
            ]
            Props [
                Style [ Height "15px" ]
            ]
        ] [
            str model.Error
        ]
    ]

let refreshButton (dispatch : Msg -> unit) =
    Button.button [
        Button.Color IsPrimary
        Button.OnClick (fun _ -> Refresh |> dispatch)
    ] [ str "Refresh" ]

let schemaButton =
    Button.a [
        Button.Color IsInfo
        Button.Props [
            Href "schemas/lighting.json"
        ]
    ] [ str "View Schema" ]

let gitHubButton =
    Button.a [
        Button.Color IsLink
        Button.Props [
            Href "https://github.com/michaelrm97/clara"
        ]
    ] [
        img [
            Src "/GitHub-Mark-64px.png"
            Alt "GitHub"
            Style [
                Height "100%"
                MarginRight "10px"
            ]
        ]
        str "About"
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero [
        Hero.IsFullHeight
    ] [
        Hero.body [ ] [
            Container.container [ ] [
                Column.column [
                    Column.Width (Screen.All, Column.Is8)
                    Column.Offset (Screen.All, Column.Is2)
                ] [
                    Heading.p [ Heading.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [ str "Project C.L.A.R.A" ]
                    containerBox model dispatch
                    Level.level [ ] [
                        Level.left [ ] [
                            Level.item [ ] [
                                schemaButton
                            ]
                            Level.item [ ] [
                                gitHubButton
                            ]
                        ]
                        Level.right [ ] [
                            Level.item [ ] [
                                refreshButton dispatch
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
