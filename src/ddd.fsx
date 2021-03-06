(*Copyright 2016 Andrew M. Olney

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.*)

//NOTE: named ddd to avoid namespace collision with D3

#r "../node_modules/fable-core/Fable.Core.dll"
#load "../node_modules/fable-import-d3/Fable.Import.D3.fs"

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
let MyD3 = importAll<obj> "d3"
let ReactFauxDOM = importAll<obj> "react-faux-dom/lib/ReactFauxDOM"

type Datum =
    {
    Date : System.DateTime
    Close : float
}

// not sure who the original licese is; very generic code
let D3Chart ( data : Datum array ) =

    let marginTop,marginRight,marginBottom,marginLeft = 20,20,30,50
    let width = 960 - marginLeft  - marginRight
    let height = 500 - marginTop  - marginBottom

    //30-Apr-12
    let parseDate = D3.Time.Globals.format("%d-%b-%y").parse

    let x = 
        D3.Time.Globals.scale<float,float>()
            .range([|0.0; float width|])

    let y = 
        D3.Scale.Globals.linear()
            .range([|float height; 0.0|])

    let xAxis = 
        D3.Svg.Globals.axis()
            .scale(x)
            .orient("bottom")

    let yAxis = 
        D3.Svg.Globals.axis()
            .scale(y)
            .orient("left")

    let line2 = 
        D3.Svg.Globals.line<Datum>() 
            .x( System.Func<Datum,float,float>(fun d _ -> x.Invoke(d.Date) ) )
            .y( System.Func<Datum,float,float>(fun d _ -> y.Invoke(d.Close) ) )

    //this is a dynamic version of the typed line above
    //most of the below goes dynamic, especially for things like attr, which otherwise would need erasable types
    let line =
        D3.Svg.Globals.line() 
            ?x( fun d -> x$(d.Date ))
            ?y( fun d -> y$(d.Close ))

    let node  = ReactFauxDOM?createElement("svg") :?>  Browser.EventTarget
    let svg = 
        D3.Globals.select(node)
            ?attr("width", width + marginLeft + marginRight )
            ?attr("height", height + marginTop + marginBottom )
            ?append("g")
            ?attr("transform", "translate(" + marginLeft.ToString() + "," + marginTop.ToString() + ")" )


    //D3.Globals.Extent doesn't have good method for DateTime, so I used dynamic
    x?domain$( MyD3?extent( data, fun d -> d.Date) ) |> ignore

    //looks like extent is improperly returning a tuple instead of an array so we restructure
    let yMin,yMax = D3.Globals.extent<Datum>( data, System.Func<Datum, float, float>(fun d _ -> d.Close))
    ignore <| y.domain( [|yMin;yMax|] )

    svg?append("g")
        ?attr("class",  "x axis")
        ?attr("transform", "translate(0," + height.ToString() + ")") 
        ?call(xAxis) 
        |> ignore

    svg?append("g")
        ?attr("class", "y axis")
        ?call(yAxis)
        ?append("text")
        ?attr("transform", "rotate(-90)")
        ?attr("y", 6)
        ?attr("dy", ".71em")
        ?style("text-anchor", "end")
        ?text( "Price ($)")
        |> ignore

    svg?append("path")
        ?datum( data )
        ?attr("class", "line")
        ?attr("fill", "none")
        ?attr("stroke", "#000")
        ?attr("d", line)  
        |> ignore
    node?toReact() 

// ----------------------------------------------------------------------
//force directed graph
// Modal Logic Playground is a creation of Ross Kirsling, released under the MIT License. 
//-----------------------------------------------------------------------
(*** define:arrayhacks ***)
[<Fable.Core.Emit("$0.push($1)")>]
let push (sb:'a[]) (v:'a) = failwith "js"
[<Fable.Core.Emit("$0.join($1)")>]
let join (sb:'a[]) (sep:string) = failwith "js"

type ``[]``<'a> with
  member x.push(v) = push x v
  member x.join(s) = join x s


//  - nodes are known by 'id', not by index in array.
//  - reflexive edges are indicated on the node (as a bold black circle).
type Node =
    {
        id : int
        reflexive : bool
        mutable x : float
        mutable y : float
    }
let emptyNode = {id=0; reflexive=false;x=0.0;y=0.0}

//  - links are always source < target; edge directions are set by 'left' and 'right'.
type Link =
    {
        source : Node
        target : Node
        left : bool
        right : bool
    }
let emptyLink = {source = emptyNode; target = emptyNode; left=false; right = false}
let inline rand() = JS.Math.random()
let nodes = 
    [|
        {id=0; reflexive=false; x= 10.0 * rand() ;y=10.0 * rand()}
        {id=1; reflexive=true; x=20.0 * rand();y=20.0 * rand()}
        {id=2; reflexive=false; x=30.0 * rand();y=30.0 * rand()}
    |]
let links = 
    [|
        {source=nodes.[0]; target= nodes.[1]; left=false; right=true}
        {source=nodes.[1]; target= nodes.[2]; left=false; right=true}
    |]
//let D3Graph ( nodes : Node array) (links : Link array) =
let D3Graph() =

    let mutable lastNodeId = (nodes |> Array.maxBy( fun x -> x.id )).id
    let width, height = 960,500
    let colors = MyD3?scale?category10();

    let fauxNode  = ReactFauxDOM?createElement("svg") :?>  Browser.EventTarget
    let svg = 
        D3.Globals.select(fauxNode)
            ?attr("width", width )
            ?attr("height", height )
            ?attr("OnContextMenu", "return false;" )

    //define arrow markers for graph links
    svg
        ?append("svg:defs")
        ?append("svg:marker")
        ?attr("id", "end-arrow")
        ?attr("viewBox", "0 -5 10 10")
        ?attr("refX", 6)
        ?attr("markerWidth", 3)
        ?attr("markerHeight", 3)
        ?attr("orient", "auto")
        ?append("svg:path")
        ?attr("d", "M0,-5L10,0L0,5")
        ?attr("fill", "#000")
        |> ignore

    svg
        ?append("svg:defs")
        ?append("svg:marker")
        ?attr("id", "start-arrow")
        ?attr("viewBox", "0 -5 10 10")
        ?attr("refX", 4)
        ?attr("markerWidth", 3)
        ?attr("markerHeight", 3)
        ?attr("orient", "auto")
        ?append("svg:path")
        ?attr("d", "M10,-5L0,0L10,5")
        ?attr("fill", "#000")
        |> ignore

    // line displayed when dragging new nodes
    let drag_line = 
        svg
            ?append("svg:path")
            ?attr("class", "link dragline hidden")
            ?attr("d", "M0,0L0,0");

    // handles to link and node element groups
    let mutable path = svg?append("svg:g")?selectAll("path")
    let mutable circle = svg?append("svg:g")?selectAll("g");

     // update force layout (called automatically each iteration)
    let tick() =
        // draw directed edges with proper padding from node centers
        path?attr("d", fun (d:Link) ->
            let deltaX = d.target.x - d.source.x 
            let deltaY = d.target.y - d.source.y
            let dist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY)
            let normX = deltaX / dist
            let normY = deltaY / dist
            let sourcePadding = if d.left then 17.0 else 12.0
            let targetPadding = if d.right then 17.0 else 12.0
            let sourceX = d.source.x + (sourcePadding * normX)
            let sourceY = d.source.y + (sourcePadding * normY)
            let targetX = d.target.x - (targetPadding * normX)
            let targetY = d.target.y - (targetPadding * normY);
            "M" + sourceX.ToString() + "," + sourceY.ToString() + "L" + targetX.ToString() + "," + targetY.ToString();
        ) |> ignore

        circle?attr("transform", fun d ->
            "translate(" + unbox<string>(d?x) + "," + unbox<string>(d?y) + ")";
        )
    //init d3 force layout
    let force = 
        D3.Layout.Globals.force()
            ?nodes( nodes )
            ?links(links)
            ?size([|width;height|])
            ?linkDistance(150)
            ?charge(-500)
            ?on("tick",tick)

    // mouse event vars
    let mutable selected_node = emptyNode
    let mutable selected_link = emptyLink
    let mutable mousedown_link = emptyLink
    let mutable mousedown_node = emptyNode
    let mutable mouseup_node = emptyNode

    let resetMouseVars() =
        mousedown_node <- emptyNode;
        mouseup_node <- emptyNode;
        mousedown_link <- emptyLink;
    

    // update graph (called when needed)
    let rec restart() =
        // path (link) group
        path <- path?data(links)

        // update existing links
        path?classed("selected", fun d ->  d = selected_link; )
            ?style("marker-start", fun (d : Link) ->  if d.left then "url(#start-arrow)" else ""; )
            ?style("marker-end", fun (d : Link) ->  if d.right then "url(#end-arrow)" else ""; )
            |> ignore

        // add new links
        path?enter()?append("svg:path")
            ?attr("class", "link")
            ?classed("selected", fun d ->  d = selected_link )
            ?style("marker-start", fun (d:Link) ->  if d.left then "url(#start-arrow)" else ""; )
            ?style("marker-end", fun (d:Link)  ->  if d.right then "url(#end-arrow)" else ""; )
            ?on("mousedown", fun d ->
                
                if  (Browser.event :?> Browser.KeyboardEvent).ctrlKey then 
                    ()
                else
                    // select link
                    mousedown_link <- d;
                    if mousedown_link = selected_link then 
                        selected_link <- emptyLink;
                    else 
                        selected_link <- mousedown_link;
                    selected_node <- emptyNode;
                    restart();
            ) |> ignore

        // remove old links
        path?exit()?remove() |> ignore


        // circle (node) group
        // NB: the function arg is crucial here! nodes are known by id, not by index!
        circle <- circle?data(nodes, fun d -> d?id; ) 

        // update existing nodes (reflexive & selected visual states)
        circle?selectAll("circle")
            ?style("fill", fun d ->  if d = selected_node then MyD3?rgb(colors$(d?id))?brighter()?toString() else colors$(d?id); )
            ?classed("reflexive", fun d ->  d?reflexive; )
            |> ignore

        // add new nodes
        let g = circle?enter()?append("svg:g");

        g?append("svg:circle")
            ?attr("class", "node")
            ?attr("r", 12)
            ?style("fill", fun d -> if d = selected_node then  MyD3?rgb(colors$(d?id))?brighter()?toString() else colors$(d?id); )
            ?style("stroke", fun d ->  MyD3?rgb(colors$(d?id))?darker()?toString(); )
            ?classed("reflexive", fun d ->  d?reflexive; )
            ?on("mouseover", fun d ->
                if mousedown_node = emptyNode|| d = mousedown_node then
                    ()
                else
                    // enlarge target node
                    //dealing with "this" https://hstefanski.wordpress.com/2015/10/25/responding-to-d3-events-in-typescript/
                    D3.Globals.select(Browser.event.currentTarget)?attr("transform", "scale(1.1)") |> ignore
            )
            ?on("mouseout", fun d ->
                if mousedown_node  = emptyNode || d = mousedown_node then
                    ()
                else
                // unenlarge target node
                    D3.Globals.select(Browser.event.currentTarget)?attr("transform", "") |> ignore
            )
            ?on("mousedown", fun (d) ->
                let ctrlKey = (Browser.event :?> Browser.KeyboardEvent).ctrlKey
                if ctrlKey then
                    () 
                else
                    // select node
                    mousedown_node <- d;
                    if mousedown_node = selected_node then
                        selected_node <- emptyNode;
                    else 
                        selected_node <- mousedown_node;
                    selected_link <- emptyLink;

                    // reposition drag line
                    drag_line
                        ?style("marker-end", "url(#end-arrow)")
                        ?classed("hidden", false)
                        ?attr("d", "M" + mousedown_node?x.ToString() + "," + mousedown_node?y.ToString() + "L" + mousedown_node?x.ToString() + "," + mousedown_node?y.ToString())
                        |> ignore

                    restart() |> ignore
            )
            ?on("mousedown", fun (d) ->
                let ctrlKey = (Browser.event :?> Browser.KeyboardEvent).ctrlKey
                //Browser.KeyboardEvent.prototype
                if ctrlKey then
                    () 
                else
                    // select node
                    mousedown_node <- d;
                    if mousedown_node = selected_node then
                        selected_node <- emptyNode;
                    else 
                        selected_node <- mousedown_node;
                    selected_link <- emptyLink;

                    // reposition drag line
                    drag_line
                        ?style("marker-end", "url(#end-arrow)")
                        ?classed("hidden", false)
                        ?attr("d", "M" + mousedown_node?x.ToString() + "," + mousedown_node?y.ToString() + "L" + mousedown_node?x.ToString() + "," + mousedown_node?y.ToString())
                        |> ignore

                    restart() |> ignore
            )
            ?on("mouseup", fun d ->
                if mousedown_node = emptyNode then
                    ()
                else
                    // needed by FF
                    drag_line
                        ?classed("hidden", true)
                        ?style("marker-end", "")
                        |> ignore

                    // check for drag-to-self
                    mouseup_node <- d;
                    if mouseup_node = mousedown_node then
                        resetMouseVars() |> ignore
                    else
                        // unenlarge target node
                        D3.Globals.select(Browser.event.currentTarget)?attr("transform", "") |> ignore

                        // add link to graph (update if exists)
                        // NB: links are strictly source < target; arrows separately specified by booleans
                        let mutable source = emptyNode
                        let mutable target = emptyNode
                        let mutable direction = null;

                        if (mousedown_node?id |> unbox<int> ) < ( mouseup_node?id |> unbox<int>) then
                            source <- mousedown_node;
                            target <- mouseup_node;
                            direction <- "right"
                        else 
                            source <- mouseup_node;
                            target <- mousedown_node;
                            direction <- "left"
                        

                        let mutable link = (links?filter( fun (l : Link)-> l.source = (source |> unbox<Node>) && l.target = (target|> unbox<Node>)) |> unbox<Link array>).[0]

                        if link <> emptyLink then
                            link?(direction) <- true;
                        else 
                            link <- {source = source ; target =target ; left = false; right = false};
                            link?(direction) <- true;
                            links.push(link);
                        

                        // select new link
                        selected_link <- link;
                        selected_node <- emptyNode;
                        restart();
            ) 
            |> ignore

        // show node IDs
        g?append("svg:text")
            ?attr("x", 0)
            ?attr("y", 4)
            ?attr("class", "id")
            ?text(fun d ->  d?id; )
            |> ignore

        // remove old nodes
        circle?exit()?remove() |> ignore

        // set the graph in motion
        force?start() |> ignore

    let mousedown() =
        // prevent I-bar on drag
        //d3?event?preventDefault();
        let e = (Browser.event :?> Browser.KeyboardEvent).ctrlKey 
        //MyD3?event?ctrlKey |> unbox<bool>
        //Browser.KeyboardEvent.prototype
        //Browser.MouseEvent.prototype

        // because :active only works in WebKit?
        svg?classed("active", true) |> ignore

        if e|| mousedown_node <> emptyNode || mousedown_link <> emptyLink then
            ()
        else
            // insert new node at point
            let x,y = D3.Globals.mouse(Browser.event.currentTarget)
            let  node = {id = lastNodeId + 1; reflexive = false; x = x ; y = y};
            nodes.push(node);

            restart();
    

    let mousemove() =
        if mousedown_node = emptyNode then
            ()
        else
            // update drag line
            let x,y = D3.Globals.mouse(Browser.event.currentTarget)
            drag_line?attr("d", "M" + ( mousedown_node?x |> unbox<string>) + "," + (mousedown_node?y |> unbox<string>) + "L" + x.ToString() + "," + y.ToString() ) |> ignore

            restart();
            

    let mouseup() =
        if mousedown_node <> emptyNode then
            // hide drag line
            drag_line
                ?classed("hidden", true)
                ?style("marker-end", "") |> ignore
            ()

        // because :active only works in WebKit?
        svg?classed("active", false) |> ignore

        // clear mouse event vars
        resetMouseVars();

    let spliceLinksForNode(node) =
            let toSplice = links?filter(fun (l : Link)->
                 (l.source = node || l.target = node);
            )
            toSplice?map(fun l ->
                links?splice(links?indexOf(l), 1);
            )
            |> ignore


    // only respond once per keydown
    let mutable lastKeyDown = -1;

    let keydown() =
        Browser.Event.prototype.preventDefault()

        let keyCode = int (Browser.event :?> Browser.KeyboardEvent).keyCode
        //Browser.KeyboardEvent.prototype

        if lastKeyDown <> -1 then 
            ()
        else
            lastKeyDown <- int keyCode;

            // ctrl
            if keyCode = 17 then
                circle?call(force?drag) |> ignore
                svg?classed("ctrl", true) |> ignore
            
            if selected_node <> emptyNode && selected_link <> emptyLink then
                ()
            else
                match keyCode with
                //backspace or delete
                | 8 | 46 ->
                    if selected_node <> emptyNode then
                        nodes?splice(nodes?indexOf(selected_node), 1) |> ignore
                        spliceLinksForNode(selected_node) |> ignore
                        ()
                    else if selected_link <> emptyLink then
                        links?splice(links?indexOf(selected_link), 1) |> ignore
                        ()

                    selected_link <- emptyLink;
                    selected_node <- emptyNode;
                    restart();
                //B
                | 66 ->
                    if selected_link <> emptyLink then
                        // set link direction to both left and right
                        selected_link <- {selected_link with left = true; right = true}
                    restart();
                //L
                | 76 ->
                    if selected_link <> emptyLink then
                        // set link direction to left only
                        selected_link <- {selected_link with left = true; right = false}
                    restart();
                // R
                | 82 ->
                    if selected_node <> emptyNode then
                        // toggle node reflexivity
                        selected_node <- {selected_node with reflexive = not selected_node.reflexive}
                    else if selected_link  <> emptyLink then
                        // set link direction to right only
                        selected_link <- {selected_link with left = false; right = true}
                    restart();

    let keyup() =
        lastKeyDown <- -1;
        
        //let e = Browser.KeyboardEvent.prototype
        let keyCode = int (Browser.event :?> Browser.KeyboardEvent).keyCode
        // ctrl
        if keyCode = 17 then
            circle
                ?on("mousedown.drag", null)
                ?on("touchstart.drag", null)
                |> ignore
            svg?classed("ctrl", false) |> ignore


    // app starts here
    svg?on("mousedown", mousedown)
        ?on("mousemove", mousemove)
        ?on("mouseup", mouseup)
        ?on("keydown", keydown)
        ?on("keyup", keyup)
        |> ignore
    restart();

    fauxNode?toReact() 
//-------------------------------------------------------------------------------