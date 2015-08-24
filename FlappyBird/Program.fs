#if INTERACTIVE
#r "PresentationCore.dll"
#r "PresentationFramework.dll"
#r "System.Xaml.dll"
#r "UIAutomationTypes.dll"
#r "WindowsBase.dll"
#endif

open System
open System.IO
open System.Windows
open System.Windows.Controls
open System.Windows.Input
open System.Windows.Media
open System.Windows.Shapes
open System.Windows.Media.Imaging

let PIPE_GAP_X = 300
let PIPE_GAP_Y = 150
let PIPE_WIDTH = 50

/// Converts specified bitmap to an image
let toImage (bitmap:#BitmapSource) =
   let w, h = float bitmap.PixelWidth, float bitmap.PixelHeight  
   Image(Source=bitmap,Stretch=Stretch.Fill,Width=w,Height=h) 
/// Loads image from file if it exists or the url otherwise
let load file url =
   let path = Path.Combine(__SOURCE_DIRECTORY__, file)
   let uri = 
      if File.Exists(path) 
      then Uri(path, UriKind.Relative)
      else Uri(url, UriKind.Absolute)
   let bitmap = BitmapImage()
   bitmap.BeginInit()
   bitmap.UriSource <- uri
   bitmap.EndInit()
   bitmap

let bg = 
   load "bg.png" "http://flappycreator.com/default/bg.png"
   |> toImage
let ground = 
   load "ground.png" "http://flappycreator.com/default/ground.png"
   |> toImage
let tube1 = load "tube1.png" "http://flappycreator.com/default/tube1.png"
let tube2 = load "tube2.png" "http://flappycreator.com/default/tube2.png"
let bird_sing = 
   load "bird_sing.png" "http://flappycreator.com/default/bird_sing.png"
   |> toImage
let scroll = ref 0.0
let canvas = Canvas()
let move image (x,y) =
   Canvas.SetLeft(image, x)
   Canvas.SetTop(image, y)
let add image (x,y) = 
   canvas.Children.Add(image) |> ignore
   move image (float x, float y)

/// Bird type
type Bird = { X:float; Y:float; VY:float; IsAlive:bool }
/// Respond to flap command
let flap (bird:Bird) = { bird with VY = - System.Math.PI }
/// Applies gravity to bird
let gravity (bird:Bird) = { bird with VY = bird.VY + 0.1 }
/// Applies physics to bird
let physics (bird:Bird) = { bird with Y = bird.Y + bird.VY }
/// Updates bird with gravity & physics

let crashBox(bird: Bird, points : Point * Point) =
    let p1 = fst points  
    let p2 = snd points
    let X = bird.X  - float (!scroll) + bird_sing.ActualWidth
    let Y = bird.Y
    let dirVec = (1., bird.VY)
    if p1.X <  X &&  X < p2.X then
        match ( X, Y,bird.VY) with
        | (x,y,vy) when (Y > p1.Y && p1.Y < (Y+bird_sing.ActualHeight) &&  (Y+bird_sing.ActualHeight) < p2.Y) -> 
            { bird with IsAlive = false }
        | (x,y,vy) when (p1.Y < (Y+bird_sing.ActualHeight) &&  (Y+bird_sing.ActualHeight) < p2.Y) -> 
            { bird with Y = p1.Y-bird_sing.ActualHeight; IsAlive = false } 
        | (x,y,vy) when (vy < 0. && p1.Y <  Y &&  Y < p2.Y) -> 
            { bird with Y = p2.Y; IsAlive = false } 
        | (x,y,vy) when (vy < 0. && p1.Y >  Y &&  Y > p2.Y) -> 
            { bird with Y = p2.Y; IsAlive = false } 
        | _ -> bird
    else bird
let crashFloor (bird : Bird) =
    let max_height = 360.
    crashBox(bird, (new Point(-infinity, max_height),new Point(+infinity,+infinity)))

let crashTop (bird: Bird) = { bird with Y = System.Math.Max(bird.Y, 0.)}
               
/// Generates the level's tube positions
let generateLevel n =
   let rand = System.Random()
   [for i in 1..n -> 50+(i*PIPE_GAP_X), 32+rand.Next(160)]

let level = generateLevel 100

let topBoxList = level |> List.map( fun (x, y)->
                                           let width =  PIPE_WIDTH
                                           let p1 = new Point(float x,-infinity)
                                           let p2 = new Point(float x + float width, float y ) 
                                           (p1, p2))

let bottomBoxList = level |> List.map( fun (x, y)->
                                           let width =  PIPE_WIDTH
                                           let p1 = new Point(float x, float (y + PIPE_GAP_Y))
                                           let p2 = new Point(float x + float width, +infinity) 
                                           (p1, p2))

let boxList = List.append topBoxList bottomBoxList

let levelList = boxList |> List.map( fun x ->
                                           let f( bird: Bird) = crashBox(bird, x)
                                           f)

let crashPipe (bird: Bird) : Bird = levelList  |> List.fold(fun a b -> a |> b) bird        

let update = gravity >> physics >> crashFloor >> crashTop >> crashPipe

add bg (0,0)
add bird_sing (30,150)
// Level's tubes
let tubes =
   [for (x,y) in level ->
      let tube1 = toImage tube1
      let tube2 = toImage tube2
      add tube1 (x,y-320)
      add tube2 (x,y+PIPE_GAP_Y)
      (x,y), tube1, tube2]
add ground (0,360)


let flappy = ref { X = 30.0; Y = 150.0; VY = 0.0; IsAlive=true }
let flapme () = if (!flappy).IsAlive then flappy := flap !flappy

let window = Window(Title="Flap me -  Hit S to Start",Width=288.0,Height=440.0)
window.Content <- canvas
let dscroll = ref 1.0
window.MouseDown.Add(fun _ -> flapme())

let mutable running =  true
window.KeyDown.Add(fun args -> if args.Key = Key.Space then flapme() )
window.KeyDown.Add(fun args -> if args.Key = Key.S then running <- true)
                                    
                                    
CompositionTarget.Rendering.Add(fun _ ->
                                       if running then
                                            if (!flappy).IsAlive then
                                                flappy := update !flappy
                                                let bird = !flappy
                                                move bird_sing (bird.X, bird.Y)
                                                for ((x,y),tube1,tube2) in tubes do
                                                    move tube1 ((float x + !scroll),float (y-320))
                                                    move tube2 ((float x + !scroll),float (y+PIPE_GAP_Y))
                                                scroll := !scroll - !dscroll
                                                dscroll := !dscroll + 0.0015
                                            else
                                                dscroll := 1.0
                                                scroll := 0.0
                                                flappy :=  { X = 30.0; Y = 150.0; VY = 0.0; IsAlive=true }
                                                running <- false
                                        )
window.Show()



// Update scene
