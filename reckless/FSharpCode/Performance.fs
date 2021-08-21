module FSharpCode.Performance

open System.Diagnostics

let benchmark f =
    let stopwatch = new Stopwatch()
    stopwatch.Start()
    f()
    stopwatch.Stop()
    stopwatch.ElapsedMilliseconds