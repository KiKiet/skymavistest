using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GameMazeManager : MonoBehaviour
{
    [SerializeField] UILayer uiLayer;
    [SerializeField] GameObject[] sampleObjects;
    [SerializeField] AxieObject axie;
    Mesh mesh;
    MazeState mazeState;
    bool isPlaying;

    float turnDelay;
    float autoMoveDelay;
    private int count;
    private List<int2> pathPos;
    private List<Vector2Int> keyPos; 
    Dictionary<string, List<IMazeObject>> pools = new Dictionary<string, List<IMazeObject>>();

    private void Awake()
    {
        keyPos = new List<Vector2Int>();
        mesh = new Mesh
        {
            name = "Procedural Mesh"
        };
        GetComponent<MeshFilter>().mesh = mesh;

        AxieMixer.Unity.Mixer.Init();
        string axieId = PlayerPrefs.GetString("selectingId", "2727");
        string genes = PlayerPrefs.GetString("selectingGenes", "0x2000000000000300008100e08308000000010010088081040001000010a043020000009008004106000100100860c40200010000084081060001001410a04406");
        axie.figure.SetGenes(axieId, genes);

        mazeState = new MazeState();
        ResetGame();
    }

    IMazeObject GetGameObject(string key)
    {
        var sample = sampleObjects.FirstOrDefault(x => x.name == key);
        if (sample == null) return null;

        if (!pools.ContainsKey(key))
        {
            pools.Add(key, new List<IMazeObject>());
        }
        var lst = pools[key];

        var mazeGO = lst.FirstOrDefault(x => !x.gameObject.activeSelf);
        if(mazeGO == null)
        {
            var go = Instantiate(sample);
            mazeGO = (IMazeObject)go.GetComponent(typeof(IMazeObject));
            lst.Add(mazeGO);
        }
        else
        {
            mazeGO.gameObject.SetActive(true);
        }
        return mazeGO;
    }

    void ResetGame()
    {
        this.uiLayer.SetResultFrame(false);

        mazeState.LoadMaps(MapPool.FLOOR_MAPS);

        this.uiLayer.SetInventoryStates(this.mazeState.axie.consumableItems);

        this.axie.SetMapPos(this.mazeState.axie.mapX, this.mazeState.axie.mapY);

        this.EnterFloor(this.mazeState.currentFloorIdx);
        this.isPlaying = true;
        this.turnDelay = 0.1f;
        this.autoMoveDelay = 5;
    }

    void EnterFloor(int idx)
    {
        count=0;
        pathPos = new List<int2>();
        this.mazeState.currentFloorIdx = idx;
        var floorMap = this.mazeState.floors[this.mazeState.currentFloorIdx];

        foreach (var p in pools)
        {
            foreach (var q in p.Value)
            {
                q.gameObject.SetActive(false);
            }
        }

        mesh.Clear();
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < MazeState.MAP_SIZE * 2 + 1; i++)
        {
            for (int j = 0; j < MazeState.MAP_SIZE * 2 + 1; j++)
            {
                int x = (int)(j / 2);
                int y = (int)(i / 2);
                int val = floorMap.map[i][j];
                if (i % 2 == 1 && j % 2 == 0)
                {
                    if (val == MazeState.MAP_CODE_WALL)
                    {
                        DrawRect((x - 6), (y - 6), 0.05f, 1f, vertices, colors, triangles);
                    }
                }
                else if (i % 2 == 0 && j % 2 == 1)
                {
                    if (val == MazeState.MAP_CODE_WALL)
                    {
                        DrawRect((x - 6), (y - 6), 1f, 0.05f, vertices, colors, triangles);
                    }
                }
                else if (i % 2 == 1 && j % 2 == 1)
                {
                    if (val == MazeState.MAP_CODE_END)
                    {
                        DrawRect((x - 6 + 0.2f), (y - 6 + 0.2f), 0.6f, 0.6f, vertices, colors, triangles);
                    }
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.colors = colors.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();

        foreach (var itemState in floorMap.itemStates){
            if (!itemState.available) continue;
            if (itemState.code >= MazeState.MAP_CODE_KEY_A && itemState.code <= MazeState.MAP_CODE_KEY_B)
            {
                var key = GetGameObject("KeyObject") as KeyObject;
                if (key != null)
                {
                    key.SetMapPos(itemState.mapX, itemState.mapY);
                    key.Setup(itemState.code - MazeState.MAP_CODE_KEY_A);
                    keyPos.Add(new Vector2Int(itemState.mapX, itemState.mapY));
                }
            }
        }

        foreach (var doorState in floorMap.doorStates){
            if (!doorState.locked) continue;
            var door = GetGameObject("DoorObject") as DoorObject;
            if (door != null)
            {
                door.SetMapPos((int)(doorState.colMapX / 2),(int)(doorState.colMapY / 2));
                door.Setup(doorState.level, doorState.colMapX, doorState.colMapY);
            }
        }

        this.axie.SetMapPos(this.mazeState.axie.mapX, this.mazeState.axie.mapY);
    }

    void DrawRect(float x, float y, float w, float h, List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        int off = vertices.Count;

        vertices.Add(new Vector3(x, y, 0f));
        vertices.Add(new Vector3(x + w, y, 0f));
        vertices.Add(new Vector3(x, y + h, 0f));
        vertices.Add(new Vector3(x + w, y + h, 0f));

        colors.Add(Color.gray);
        colors.Add(Color.gray);
        colors.Add(Color.gray);
        colors.Add(Color.gray);

        triangles.Add(off);
        triangles.Add(off + 1);
        triangles.Add(off + 2);

        triangles.Add(off + 3);
        triangles.Add(off + 1);
        triangles.Add(off + 2);
    }

    private void MoveAxie(int dx, int dy)
    {
        //if (!this.isPlaying || this.axie == null) return;
        int nx = this.mazeState.axie.mapX + dx;
        int ny = this.mazeState.axie.mapY + dy;
        int colMapX, colMapY;
        if (dx != 0)
        {
            colMapX = (this.mazeState.axie.mapX + (dx == 1 ? 1 : 0)) * 2;
            colMapY = this.mazeState.axie.mapY * 2 + 1;
        }
        else
        {
            colMapX = this.mazeState.axie.mapX * 2 + 1;
            colMapY = (this.mazeState.axie.mapY + (dy == 1 ? 1 : 0)) * 2;
        }
        var logs = this.mazeState.OnMove(dx, dy);
        if (!logs.ContainsKey("action")) return;

        string action = logs["action"];

        switch (action)
        {
            case "move":
                this.axie.SetMapPos(this.mazeState.axie.mapX, this.mazeState.axie.mapY);
                break;
            case "enterFloor":
                if (this.mazeState.isWon)
                {
                    this.GameOver(true);
                }
                else
                {
                    this.EnterFloor(this.mazeState.currentFloorIdx);
                }
                this.axie.SetMapPos(this.mazeState.axie.mapX, this.mazeState.axie.mapY);
                break;
            case "gainKey":
                this.SyncKey(nx, ny);
                this.axie.SetMapPos(this.mazeState.axie.mapX, this.mazeState.axie.mapY);
                keyPos.Remove(new Vector2Int(nx,ny));
                break;
            case "unlockDoor":
                this.SyncDoor(colMapX, colMapY);
                break;

            default:
                break;
        }
    }

    private void SyncKey(int mapX, int mapY)
    {
        var floorMap = this.mazeState.floors[this.mazeState.currentFloorIdx];
        var key = this.pools["KeyObject"].Find(x => x.mapPos.x == mapX && x.mapPos.y == mapY);
        var itemState = floorMap.itemStates.Find(x => x.mapX == mapX && x.mapY == mapY);
        if (key == null || itemState == null) return;
        if (!itemState.available)
        {
            key.gameObject.SetActive(false);
        }
        this.uiLayer.SetInventoryStates(this.mazeState.axie.consumableItems);
    }

    private void SyncDoor(int colMapX, int colMapY)
    {
        var door = this.pools["DoorObject"].Find(x => (x as DoorObject).colMapPos.x == colMapX && (x as DoorObject).colMapPos.y == colMapY);
        if (door == null) return;

        var floorMap = this.mazeState.floors[this.mazeState.currentFloorIdx];
        if (floorMap.map[colMapY][colMapX] == MazeState.MAP_CODE_CLEAR)
        {
            door.gameObject.SetActive(false);
        }
        this.uiLayer.SetInventoryStates(this.mazeState.axie.consumableItems);
    }

    private void GameOver(bool isWon)
    {
        this.isPlaying = false;
        this.uiLayer.SetResultFrame(true);
        this.uiLayer.SetResultText(isWon);
    }

    private void Update()
    {
        if (!isPlaying)
        {
            if (Input.anyKeyDown)
            {
                this.ResetGame();
            }
            return;
        }

        if (Input.GetKeyDown("1"))
        {
            EnterFloor(0);
        }
        else if (Input.GetKeyDown("2"))
        {
            EnterFloor(1);
        }
        else if (Input.GetKeyDown("3"))
        {
            EnterFloor(2);
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            this.autoMoveDelay = 5;
            MoveAxie(-1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            this.autoMoveDelay = 5;
            MoveAxie(1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            this.autoMoveDelay = 5;
            MoveAxie(0, 1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            this.autoMoveDelay = 5;
            MoveAxie(0, -1);
        }

        this.autoMoveDelay -= Time.deltaTime;
        if (this.autoMoveDelay <= 0)
        {
            this.turnDelay -= Time.deltaTime;
            if (this.turnDelay <= 0)
            {
                this.turnDelay = 0.25f;
                this.OnSimulateTurn();
            }
        }
    }

    //***************YOUR CODE HERE**************************/
    void OnSimulateTurn()
    {
        //Do you check and give the action to reach the goal
        var floorMap = this.mazeState.floors[this.mazeState.currentFloorIdx];
        Vector2Int targetPos = Vector2Int.zero;
        if (keyPos.Count == 0){
            for (int y = 0; y < MazeState.MAP_SIZE; y++)
            {
                for (int x = 0; x < MazeState.MAP_SIZE; x++)
                {
                    int roomVal = this.mazeState.GetRoomValue(x, y);
                    if (roomVal == MazeState.MAP_CODE_END)
                    {
                        targetPos = new Vector2Int(x, y); 
                    }
                }
            }
            if(pathPos.Count==0){
                pathPos = PathFinding(new int2(mazeState.axie.mapX, mazeState.axie.mapY),new int2(targetPos.x, targetPos.y));
            }else{
                if(count<pathPos.Count){
                    this.MoveAxie(pathPos[count].x-mazeState.axie.mapX, pathPos[count].y-mazeState.axie.mapY);
                    count++;
                }
                this.MoveAxie(pathPos[count-1].x-mazeState.axie.mapX, pathPos[count-1].y-mazeState.axie.mapY);
            }
        }else{
            // foreach (var itemState in floorMap.itemStates){
            //     if (!itemState.available) continue;
            //     if (itemState.code >= MazeState.MAP_CODE_KEY_A && itemState.code <= MazeState.MAP_CODE_KEY_B)
            //     {
            //         //Debug.Log($"{itemState.mapX},{itemState.mapY}");
                    
            //     }
            // }
            if(pathPos.Count==0){
                pathPos = PathFinding(new int2(mazeState.axie.mapX, mazeState.axie.mapY),new int2(keyPos[0].x, keyPos[0].y));
            }else{
                if(count<pathPos.Count){
                    this.MoveAxie(pathPos[count].x-mazeState.axie.mapX, pathPos[count].y-mazeState.axie.mapY);
                    count++;
                }
                if(count==pathPos.Count){
                    count=0;
                    pathPos.Clear();
                }
            }
        }
        


        Debug.Log($"curPos: {mazeState.axie.mapX},{mazeState.axie.mapY} targetPos: {targetPos.x},{targetPos.y} Item remain: {floorMap.itemStates.Count}");
        // int ranVal = Random.Range(0, 4);
        // if (ranVal == 0 && mazeState.TestMove(-1, 0) == MoveResult.Valid)
        // {
        //     this.MoveAxie(-1, 0);
        // }
        // else if (ranVal == 1 && mazeState.TestMove(1, 0) == MoveResult.Valid)
        // {
        //     this.MoveAxie(1, 0);
        // }
        // else if (ranVal == 2 && mazeState.TestMove(0, -1) == MoveResult.Valid)
        // {
        //     this.MoveAxie(0, -1);
        // }
        // else if (ranVal == 3 && mazeState.TestMove(0, 1) == MoveResult.Valid)
        // {
        //     this.MoveAxie(0, 1);
        // }
        // List<Node> path = FindPath(mazeState.axie.mapX, mazeState.axie.mapY, targetPos.x, targetPos.y);
        // if (path != null){
        //     for (int i=0; i < path.Count - 1; i++){
        //         Debug.Log(path[i].ToString());
        //         this.MoveAxie(mazeState.axie.mapX-path[i].x,mazeState.axie.mapY-path[i].y);
        //     }
        // }
    }
    private List<int2> PathFinding (int2 sPos, int2 ePos){
        int2 gridSize = new int2(MazeState.MAP_SIZE,MazeState.MAP_SIZE);
        NativeArray<Node> nodeArr = new NativeArray<Node>(gridSize.x*gridSize.y,Allocator.Temp);
        
        for (int x = 0; x < gridSize.x; x++){
            for (int y = 0; y < gridSize.y; y++){
                Node node = new Node();
                node.x=x;
                node.y=y;
                node.index = CalIndex(x,y,gridSize.x);
                node.gCost = int.MaxValue;
                node.hCost = CalHCost(new int2(x,y),ePos);
                node.CalFCost();
                node.isWalkable=true;
                node.cameFromNode=-1;
                nodeArr[node.index]=node;
            }
        }

        NativeArray<int2>neighbourOffsetArray = new NativeArray<int2>(new int2[]{
            new int2(-1,0),
            new int2(0,-1),
            new int2(1,0),
            new int2(0,1),
        },Allocator.Temp);
        int sNodeIndex = CalIndex(sPos.x,sPos.y,gridSize.x);
        Node eNode = nodeArr[CalIndex(ePos.x,ePos.y,gridSize.x)];
        eNode.gCost = 0;
        eNode.CalFCost();
        nodeArr[eNode.index]=eNode;

        NativeList<int> openList = new NativeList<int>(Allocator.Temp);
        NativeList<int> closedList = new NativeList<int>(Allocator.Temp);

        openList.Add(eNode.index);
        while (openList.Length > 0){
            int curNodeIndex = GetLowestFCost(openList, nodeArr);
            Node curNode = nodeArr[curNodeIndex];
            if (curNodeIndex == sNodeIndex){
                break;
            }
            for (int i=0;i<openList.Length;i++){
                if(openList[i]==curNodeIndex){
                    openList.RemoveAtSwapBack(i);
                    break;
                }
            }

            closedList.Add(curNodeIndex);
            for (int i = 0; i < neighbourOffsetArray.Length; i++){
                int2 neighbourOffset = neighbourOffsetArray[i];
                int2 neighbourPos = new int2(Mathf.Abs(curNode.x + neighbourOffset.x), Mathf.Abs(curNode.y + neighbourOffset.y));
                
                if (mazeState.TestMoveCurNode(new int2(curNode.x,curNode.y),neighbourOffset.x,neighbourOffset.y)==MoveResult.Invalid && IsPosInGrid(neighbourPos,gridSize)){
                    Node unWalkable = nodeArr[CalIndex(neighbourPos.x,neighbourPos.y,gridSize.x)];
                    unWalkable.SetIsWalkable(false);
                    nodeArr[CalIndex(neighbourPos.x,neighbourPos.y,gridSize.x)]=unWalkable;
                }
                else if(IsPosInGrid(neighbourPos,gridSize)){
                    Node unWalkable = nodeArr[CalIndex(neighbourPos.x,neighbourPos.y,gridSize.x)];
                    unWalkable.SetIsWalkable(true);
                    nodeArr[CalIndex(neighbourPos.x,neighbourPos.y,gridSize.x)]=unWalkable;
                }
                
                if (!IsPosInGrid(neighbourPos,gridSize)){
                    continue;
                }
                int neighbourNodeIndex = CalIndex(neighbourPos.x,neighbourPos.y,gridSize.x);
                if(closedList.Contains(neighbourNodeIndex)){
                    continue;
                }

                Node neighbourNode = nodeArr[neighbourNodeIndex];
                if(!neighbourNode.isWalkable){
                    continue;
                }
                int2 curNodePos = new int2(curNode.x,curNode.y);
                int tentativeGCost = curNode.gCost + CalHCost(curNodePos,neighbourPos);
                if(tentativeGCost<neighbourNode.gCost){
                    neighbourNode.cameFromNode = curNodeIndex;
                    neighbourNode.gCost = tentativeGCost;
                    neighbourNode.CalFCost();
                    nodeArr[neighbourNodeIndex]=neighbourNode;
                    if(!openList.Contains(neighbourNode.index)){
                        openList.Add(neighbourNode.index);
                    }
                }
            }
        }
        Node sNode=nodeArr[sNodeIndex];
        List<int2> result = new List<int2>();
        if(sNode.cameFromNode==-1){
            Debug.Log("not found");
        }else{
            NativeList<int2> path = CalPath(nodeArr,sNode);
            foreach (int2 pathPos in path){
                //Debug.Log(pathPos);
                //if(mazeState.axie.mapX-pathPos.x!=0&&mazeState.axie.mapY-pathPos.y!=0)
                //this.MoveAxie(pathPos.x-mazeState.axie.mapX, pathPos.y-mazeState.axie.mapY);
                result.Add(pathPos);
            }
            path.Dispose();
        }
        neighbourOffsetArray.Dispose();
        nodeArr.Dispose();
        openList.Dispose();
        closedList.Dispose();
        return result;
    }
    private NativeList<int2> CalPath(NativeArray<Node> nodeArr, Node sNode){
        if (sNode.cameFromNode == -1){
            return new NativeList<int2>(Allocator.Temp);
        }else{
            NativeList<int2> path = new NativeList<int2>(Allocator.Temp);
            path.Add(new int2(sNode.x, sNode.y));
            Node curNode = sNode;
            while(curNode.cameFromNode!=-1){
                Node cameFrom = nodeArr[curNode.cameFromNode];
                path.Add(new int2(cameFrom.x,cameFrom.y));
                curNode=cameFrom;
            }
            return path;
        }
    }
    private bool IsPosInGrid(int2 gridPos, int2 gridSize){
        return
            gridPos.x>=0&&
            gridPos.y>=0&&
            gridPos.x<gridSize.x&&
            gridPos.y<gridSize.y;
    }
    private int CalIndex(int x,int y, int gridWidth){
        return x+y*gridWidth;
    }
    private int CalHCost(int2 s, int2 e){
        int xDis = Mathf.Abs(s.x-e.x);
        int yDis = Mathf.Abs(s.y-e.y);
        return Mathf.Abs(xDis+yDis);
    }
    private int GetLowestFCost(NativeList<int> openList,NativeArray<Node> nodeArr){
        Node lowestCostNode = nodeArr[openList[0]];
        for (int i = 1; i < openList.Length; i++){
            Node test = nodeArr[openList[i]];
            if(test.fCost < lowestCostNode.fCost){
                lowestCostNode = test;
            }
        }
        return lowestCostNode.index;
    }
    // public List<Node> FindPath(int sX, int sY, int eX, int eY){
    //     Node startNode = new Node(sX,sY);
    //     Node endNode = new Node(eX,eY);
    //     if (startNode == null || endNode == null){
    //         return null;
    //     }
    //     openList = new List<Node> { startNode };
    //     closedList = new List<Node>();
    //     for (int x = 0; x < MazeState.MAP_SIZE; x++){
    //         for (int y = 0; y < MazeState.MAP_SIZE; y++){
    //             Node node = new Node(x,y);
    //             node.gCost=99999999;
    //             node.CalculateFCost();
    //             node.cameFromNode = null;
    //         }
    //     }
    //     startNode.gCost = 0;
    //     startNode.hCost = CalHCost(startNode,endNode);
    //     startNode.CalculateFCost();

        
    //     while(openList.Count>0){
    //         Node curNode = GetLowestFCost(openList);
    //         if (curNode == endNode){
    //             return CalPath(endNode);
    //         }

    //         openList.Remove(curNode);
    //         closedList.Add(curNode);

    //         foreach(Node neighbourNode in GetNeighbourList(curNode)){
    //             if (closedList.Contains(neighbourNode)) continue;
    //             else {
    //                 closedList.Add(neighbourNode);
    //             }
    //             int tentativeGCost = curNode.gCost + CalHCost(curNode,neighbourNode);
    //             if(tentativeGCost<neighbourNode.gCost){
    //                 neighbourNode.cameFromNode = curNode;
    //                 neighbourNode.gCost = tentativeGCost;
    //                 neighbourNode.hCost = CalHCost(neighbourNode,endNode);
    //                 neighbourNode.CalculateFCost();

    //                 if(!openList.Contains(neighbourNode)){
    //                     openList.Add(neighbourNode);
    //                 }
    //             }
    //         }
    //     }
    //     return null;
    // }

    // private int CalHCost(Node sNode, Node eNode){
    //     int xDis = Mathf.Abs(sNode.x-eNode.x);
    //     int yDis = Mathf.Abs(sNode.y-eNode.y);
    //     int dis = Mathf.Abs(xDis+yDis);
    //     return dis;
    // }
    // private Node GetLowestFCost(List<Node> Node){
    //     Node lowestF = Node[0];
    //     for (int i = 1; i < Node.Count; i++){
    //         if (Node[i].fCost < lowestF.fCost){
    //             lowestF = Node[i];
    //         }
    //     }
    //     return lowestF;
    // }
    // private List<Node> CalPath(Node eNode){
    //     List<Node> path = new List<Node>();
    //     path.Add(eNode);
    //     Node curNode = eNode;
    //     while (curNode.cameFromNode != null){
    //         path.Add(curNode.cameFromNode);
    //         curNode = curNode.cameFromNode;
    //     }
    //     path.Reverse();
    //     return path;
    // }
    // private List<Node> GetNeighbourList(Node curNode){
    //     List<Node> neighbourList = new List<Node>();

    //     if (mazeState.TestMove2(curNode, -1, 0) == MoveResult.Valid)
    //     {
    //         neighbourList.Add(GetNode(curNode.x - 1,curNode.y));
    //     }
    //     else if (mazeState.TestMove2(curNode,1, 0) == MoveResult.Valid)
    //     {
    //         neighbourList.Add(GetNode(curNode.x + 1,curNode.y));
    //     }
    //     else if (mazeState.TestMove2(curNode, 0, -1) == MoveResult.Valid)
    //     {
    //         neighbourList.Add(GetNode(curNode.x,curNode.y-1));
    //     }
    //     else if (mazeState.TestMove2(curNode, 0, 1) == MoveResult.Valid)
    //     {
    //         neighbourList.Add(GetNode(curNode.x,curNode.y + 1));
    //     }
    //     return neighbourList;
    // }
    // private Node GetNode(int x, int y){
    //     Node node = new Node(x,y);
    //     return node;
    // }
    private struct Node{
        public int x;
        public int y;
        public int index;
        public int gCost;
        public int hCost;
        public int fCost;
        public bool isWalkable;
        public int cameFromNode;
        public void CalFCost(){
            fCost=gCost+hCost;
        }
        public void SetIsWalkable(bool isWalkable){
            this.isWalkable=isWalkable;
        }
    }
}
