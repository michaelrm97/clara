namespace Storage

open LightingConfiguration

type ErrorResponse =
    { statusCode : int
      message : string }

type CurrentConfigResponse =
    { id : string
      hashCode: int }

type Storage () =
    let mutable configs : Map<string, LightingConfiguration> = Map.empty
    let mutable current : string = null

    let rec findNext (items: string list) (current: string) : string =
        match items with
        | [] -> null
        | x :: y :: _ when x = current -> y
        | _ :: x -> findNext x current

    member __.listConfigs : LightingConfigurationJsonObject list =
        List.ofSeq configs |> List.map (fun kvp -> kvp.Value.jsonObject kvp.Key)

    member __.getConfig (id: string) : Result<LightingConfigurationJsonObject, ErrorResponse> =
        match configs.TryFind id with
        | Some config -> Ok (config.jsonObject id)
        | _ -> Error { statusCode = 404
                       message = "Lighting configuration not found" }

    member __.getConfigBinary (id: string) : Result<byte list, ErrorResponse> =
        match configs.TryFind id with
        | Some config -> Ok config.binary
        | _ -> Error { statusCode = 404
                       message = "Lighting configuration not found" }

    member __.addConfig (configJsonObject: LightingConfigurationInputObject) : Result<LightingConfigurationJsonObject, ErrorResponse> =
        let id = configJsonObject.id
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

    member __.updateConfig (id: string) (configJsonObject: LightingConfigurationInputObject) : Result<unit, ErrorResponse> =
        match configs.ContainsKey configJsonObject.id with
        | true ->
            let newConfig = LightingConfiguration.convert(configJsonObject)
            match newConfig with
            | Ok c ->
                configs <- configs.Add (id, c)
                Ok ()
            | Error m -> Error { statusCode = 400
                                 message = m } 
        | false -> Error { statusCode = 404
                           message = "Lighting configuration not found" }

    member __.deleteConfig (id: string) : Result<unit, ErrorResponse> =
        match configs.ContainsKey id with
        | true -> 
            configs <- configs.Remove id
            Ok ()
        | false -> Error { statusCode = 404
                           message = "Lighting configuration not found" }

    member __.getCurrent : CurrentConfigResponse =
        match current with
        | null -> { id = null
                    hashCode = 0 }
        | _ ->
            match configs.TryFind current with
            | Some config -> { id = current
                               hashCode = config.GetHashCode() }
            | _ -> { id = null
                     hashCode = 0 }

    member this.setCurrent (id: string) : Result<CurrentConfigResponse, ErrorResponse> =
        match id with
        | null ->
            let ids = List.ofSeq configs |> List.map (fun kvp -> kvp.Key)
            match (ids, current) with
            | ([], _) ->
                current <- null
            | (_, null) ->
                // Set to first item
                current <- List.head ids
            | (_, _) ->
                // Set to next item
                let next = match findNext ids current with
                           | null -> List.head ids
                           | x -> x
                current <- next                
            Ok this.getCurrent
        | _ ->
            match configs.ContainsKey id with
            | true ->
                current <- id
                Ok this.getCurrent
            | false ->
                Error { statusCode = 400
                        message = "Lighting configuration not found" }

    member __.stop : unit =
        current <- null

    member __.insertConfig (id: string) (config: LightingConfiguration) =
        configs <- configs.Add(id, config)
