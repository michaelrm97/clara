namespace Storage

open System

open LightingConfiguration
open Shared

type Storage () =
    let mutable configs : Map<Guid, LightingConfiguration> = Map.empty
    let mutable current : CurrentConfig Option = None

    let rec findNext (items: Guid list) (current: Guid) : Guid Option =
        match items with
        | [] -> None
        | x :: y :: _ when current = x -> Some y
        | _ :: x -> findNext x current

    member __.listConfigs : LightingConfigurationJsonObject list =
        List.ofSeq configs |> List.map (fun kvp -> kvp.Value.jsonObject kvp.Key)

    member __.getConfig (id: Guid) : Result<LightingConfigurationJsonObject, ErrorResponse> =
        match configs.TryFind id with
        | Some config -> Ok (config.jsonObject id)
        | _ -> Error { statusCode = 404
                       message = "Lighting configuration not found" }

    member __.getConfigBinary (id: Guid) : Result<byte list, ErrorResponse> =
        match configs.TryFind id with
        | Some config -> Ok config.binary
        | _ -> Error { statusCode = 404
                       message = "Lighting configuration not found" }

    member __.addConfig (configJsonObject: LightingConfigurationInputObject) : Result<LightingConfigurationJsonObject, ErrorResponse> =
        let id = Guid.NewGuid()
        match configs.ContainsKey id with
        | false ->
            let newConfig = LightingConfiguration.convert(configJsonObject)
            match newConfig with
            | Ok c ->
                configs <- configs.Add (id, c)
                Ok (c.jsonObject id)
            | Error m -> Error { statusCode = 400
                                 message = m }  
        | true -> Error { statusCode = 400
                          message = "Collection already contains lighting configuration with given id" }

    member __.updateConfig (id: Guid) (configJsonObject: LightingConfigurationInputObject) : Result<LightingConfigurationJsonObject, ErrorResponse> =
        match configs.ContainsKey id with
        | true ->
            let newConfig = LightingConfiguration.convert(configJsonObject)
            match newConfig with
            | Ok c ->
                configs <- configs.Add (id, c)
                match current with
                | Some c when c.id = id ->
                    current <- Some { id = id
                                      set = DateTime.Now }
                | _ -> ()
                |> ignore
                Ok (c.jsonObject id)
            | Error m -> Error { statusCode = 400
                                 message = m } 
        | false -> Error { statusCode = 404
                           message = "Lighting configuration not found" }

    member __.deleteConfig (id: Guid) : Result<unit, ErrorResponse> =
        match configs.ContainsKey id with
        | true -> 
            configs <- configs.Remove id
            match current with
            | Some c when c.id = id -> current <- None
            | _ -> ()
            |> ignore
            Ok ()
        | false -> Error { statusCode = 404
                           message = "Lighting configuration not found" }

    member __.getCurrent : CurrentConfig Option = current

    member __.setCurrent (id: Guid Option) (start: bool) : Result<CurrentConfig Option, ErrorResponse> =
        match id with
        | None ->
            let ids = List.ofSeq configs |> List.map (fun kvp -> kvp.Key)
            match (ids, current) with
            | ([], _) ->
                current <- None
            | (_, None) ->
                if start then
                    // Set to first item
                    current <- Some { id = List.head ids
                                      set = DateTime.Now }
                else ()
            | (_, Some c) ->
                // Set to next item
                let next = match findNext ids c.id with
                            | Some x -> x
                            | None -> List.head ids
                current <- Some { id = next
                                  set = DateTime.Now }
            Ok current
        | Some i ->
            match configs.ContainsKey i with
            | true ->
                current <- Some { id = i
                                  set = DateTime.Now }
                Ok current
            | false ->
                Error { statusCode = 400
                        message = "Lighting configuration not found" }

    member __.stop : unit =
        current <- None

    member __.insertConfig  (config: LightingConfiguration) =
        configs <- configs.Add (Guid.NewGuid(), config)
