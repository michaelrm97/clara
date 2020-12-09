module Index

open System
open Elmish
open Fetch

open Shared
open Fable.Core
open Fable.SimpleJson

type LightingConfigurationUI =
    { id : Guid
      name : string
      formatted : string }

type Model =
    { Configs: LightingConfigurationUI list
      Current: Guid Option
      Selected: Guid Option
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
    | NewConfig
    | AddConfig
    | UpdateConfig
    | DeleteConfig
    | PlayConfig
    | NextConfig
    | Refresh

module ConfigJson =
    type IConfigJson =
        abstract parseConfigList : string -> LightingConfigurationUI[]
        abstract parseConfig : string -> LightingConfigurationUI

    [<Import("*", "./public/app.js")>]
    let lib: IConfigJson = jsNative

module LightingApi =
    let getConfigsAndCurrent = fun () ->
        let fetchConfigs =
            fetch "api/lighting" []
                |> Promise.bind (fun res -> res.text())
                |> Promise.map (fun raw -> ConfigJson.lib.parseConfigList raw |> List.ofArray)
                    
        let fetchCurrent =
            fetch "api/current" []
                |> Promise.bind (fun res -> res.json<CurrentConfigResponse> ())
                |> Promise.map (fun res ->
                    match res.id with
                    | null -> None
                    | id -> Some (Guid.Parse id))

        fetchConfigs
            |> Promise.bind (fun configs -> Promise.map (fun current -> configs, current) fetchCurrent)

    let addConfig = fun (input: string) ->
        fetch "api/lighting" [
            Method HttpMethod.POST
            requestHeaders [ContentType "application/json"]
            Body (unbox input)
        ]
            |> Promise.bind (fun res -> res.text())
            |> Promise.map (fun r -> Ok (ConfigJson.lib.parseConfig r))
            |> Promise.catch (fun _ -> Error "Invalid input")

    let updateConfig = fun (id: Guid, input: string) ->
        let url = sprintf "api/lighting/%s" (id.ToString())
        fetch url [
            Method HttpMethod.PUT
            requestHeaders [ContentType "application/json"]
            Body (unbox input)
        ]
            |> Promise.bind (fun res -> res.text())
            |> Promise.map (fun r -> Ok (ConfigJson.lib.parseConfig r))
            |> Promise.catch (fun _ -> Error "Invalid input")

    let deleteConfig = fun (id: Guid) ->
        let url = sprintf "api/lighting/%s" (id.ToString())
        fetch url [
            Method HttpMethod.DELETE
        ]
            |> Promise.map (fun _ -> ())

    let playConfig = fun (id: Guid) ->
        let input = {
            id = id.ToString()
        }
        fetch "api/next" [
            Method HttpMethod.POST
            requestHeaders [ContentType "application/json"]
            Body (Json.stringify input |> unbox)
        ]
            |> Promise.bind (fun res ->
                res.json<CurrentConfigResponse> ()
                |> Promise.map (fun res ->
                    match res.id with
                    | null -> None
                    | id -> Some (Guid.Parse id)))
            |> Promise.catch (fun _ -> None)

    let nextConfig = fun () ->
        fetch "api/next" [
            Method HttpMethod.POST
        ]
            |> Promise.bind (fun res -> res.json<CurrentConfigResponse> ())
            |> Promise.map (fun res ->
                match res.id with
                | null -> None
                | id -> Some (Guid.Parse id))

let findById x = fun c -> c.id = x

let init(): Model * Cmd<Msg> =
    let model =
        { Configs = []
          Current = None
          Selected = None
          Input = ""
          Error = "" }
    let cmd = Cmd.OfPromise.perform LightingApi.getConfigsAndCurrent () GotConfigs
    model, cmd

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
        { Configs = configs; Current = current; Selected = newSelected; Input = newInput; Error = "" }, Cmd.none
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
    | SetInput input ->
        { model with Input = input }, Cmd.none
    | SelectConfig selected ->
        { model with Selected = Some selected; Input = (List.find (findById selected) model.Configs).formatted }, Cmd.none
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
    | NextConfig ->
        model, Cmd.OfPromise.perform LightingApi.nextConfig () CurrentConfig
    | Refresh ->
        model, Cmd.OfPromise.perform LightingApi.getConfigsAndCurrent () GotConfigs

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
                        Button.Color IsWarning
                        Button.OnClick (fun _ -> NextConfig |> dispatch)
                        Button.Disabled (List.length model.Configs = 0)
                    ] [ str "Next" ]
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

        Textarea.textarea [
            Textarea.Value model.Input
            Textarea.OnChange (fun x -> SetInput x.Value |> dispatch)
        ] []

        if String.IsNullOrWhiteSpace model.Error then
            Text.p [ ] [ ]
        else
            Text.p [
                Modifiers [ Modifier.TextColor IsDanger ]
            ] [
                str model.Error
            ]
    ]

let refreshButton (dispatch : Msg -> unit) =
    Content.content [
        Content.Modifiers [
            Modifier.TextAlignment (Screen.All, TextAlignment.Right)
        ]
    ] [
        Button.button [
            Button.Color IsPrimary
            Button.OnClick (fun _ -> Refresh |> dispatch)
        ] [ str "Refresh" ]
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
                    refreshButton dispatch
                ]
            ]
        ]
    ]
