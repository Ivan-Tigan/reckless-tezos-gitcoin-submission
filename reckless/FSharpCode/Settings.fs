namespace FSharpCode

open EnginePrime.EnginePrime
open EnginePrime.EnginePrime.Networking
open FSharpPlus.Data
open FSharpx.Collections
open Godot
open FSharpCode.Exts
open Input

type VideoSettings = int
type AudioSettings = Map<string, bool * float32>
type AnalogStyle = LeftJoystickWASD | RightJoystickArrows | Mouse 
type ControlScheme = {
    ai: int
    aim: AnalogStyle
    move: AnalogStyle
    dashdir: AnalogStyle
    throw: InputKey list
    dash: InputKey list
}
module Controls = 
    let analog_style_to_int = function Mouse -> 0 | LeftJoystickWASD -> 1 | RightJoystickArrows -> 2
    
type ControlSettings = {
    online_controls: ControlScheme
    local_controls: ControlScheme list
}
type Settings =
    {
        video: VideoSettings
        audio: AudioSettings
        local_play_names: Networking.Identity list
        controls: ControlSettings
        use_interpolation: bool
        show_fps: bool
    } with
    static member basic =
        let new_acc = Seq.init 4 (fun _ -> AuthEd25519.AuthEd25519.new_account())
        let pubs = new_acc |> Seq.map (fun a -> CryptographicIdentity.Verified (snd a).pub)
        {
        video = 0
        audio = Map.ofSeq [ "master" , (true, 0.85f); "background", (true, 0.8f) ] 
        controls = {
            online_controls = { dashdir = Mouse; ai = 0; throw = [MouseButton Godot.ButtonList.Left]; dash = [MouseButton Godot.ButtonList.Right]; aim = Mouse; move = Mouse }
            local_controls =
                [0..4]
                |> List.map (function
                    | 0 -> { dashdir = Mouse; ai = 0; throw = [MouseButton Godot.ButtonList.Left]; dash = [MouseButton Godot.ButtonList.Right]; aim = Mouse; move = Mouse }
                    | i -> { dashdir = Mouse; ai = 1; throw = [JoystickButton Godot.JoystickList.Axis7]; dash = [JoystickButton Godot.JoystickList.Axis6]; aim = LeftJoystickWASD; move = LeftJoystickWASD }) 
        }
        local_play_names = Seq.zip ["Player 1",[]; "Player 2",[]; "Player 3",[]; "Player 4",[]] pubs |> List.ofSeq
        use_interpolation = true 
        show_fps = false }
    static member load : Settings = File.load<Settings> "user://settings.save" Settings.basic
    static member save : Settings -> unit = File.change "user://settings.save" 
type SettingsView = VideoSettingsView of VideoSettings | AudioSettingsView of AudioSettings | ControlSettingsView of ControlSettings

type BtnInputMapFs() =
    inherit Button()
    
    let mutable recording = false
    let latest_input = None