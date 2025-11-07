open System
open System.IO
open Core.SParser

[<EntryPoint>]
let main argv =
    if argv.Length > 0 then
        let filePath = argv.[0]
        let lines = File.ReadAllLines(filePath)
        for line in lines do
            printSParse line
    0
