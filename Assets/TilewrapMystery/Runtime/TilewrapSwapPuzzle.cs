using System;
using System.Collections.Generic;

namespace Chichingo.TilewrapMystery
{
    [Serializable] public sealed class SwapState
    {
        public int levelId, stage, spawnIndex, moves, extraMoves, continuesUsed;
        public string status = "playing";
        public string[] board, delivered = new string[0];
        public int[] completedSets = new int[0];
        public SwapState Clone() { return new SwapState { levelId=levelId,stage=stage,spawnIndex=spawnIndex,moves=moves,extraMoves=extraMoves,continuesUsed=continuesUsed,status=status,board=(string[])board.Clone(),delivered=(string[])delivered.Clone(),completedSets=(int[])completedSets.Clone() }; }
    }
    public sealed class SwapEvent { public string type; public int a,b,row,setIndex; public string[] items,spawned; public SwapState view; }
    public static class TilewrapSwapPuzzle
    {
        public static SwapState Create(LevelData level) { return new SwapState {levelId=level.id,board=(string[])level.start.Clone()}; }
        public static bool Adjacent(int a,int b,int count) { return a>=0&&b>=0&&a<count&&b<count&&Math.Abs(a%3-b%3)+Math.Abs(a/3-b/3)==1; }
        public static List<int> Eligible(SwapState state,LevelData level)
        {
            var pending=new List<int>();for(int i=0;i<level.sets.Length;i++)if(Array.IndexOf(state.completedSets,i)<0)pending.Add(i);
            if(pending.Count==0)return pending;
            if(level.orderMode=="hybrid")foreach(int i in pending)if(i<3)return new List<int>{i};
            if(level.orderMode!="hybrid"&&level.orderMode!="choice")return new List<int>{pending[0]};
            var active=new List<int>();foreach(int i in pending) { bool found=true;foreach(string item in level.sets[i].items)if(Array.IndexOf(state.board,item)<0)found=false;if(found)active.Add(i);if(active.Count==2)break; }return active;
        }
        public static bool Ready(SwapState state,LevelData level,out int row,out int index)
        {
            var active=Eligible(state,level);for(int r=0;r<level.rows;r++)foreach(int i in active) {
                var items=level.sets[i].items;if(state.board[r*3]==items[0]&&state.board[r*3+1]==items[1]&&state.board[r*3+2]==items[2]) {row=r;index=i;return true;}
            }row=index=-1;return false;
        }
        public static bool TrySwap(SwapState before,LevelData level,int a,int b,out SwapState state,out List<SwapEvent> events)
        {
            state=before.Clone();events=new List<SwapEvent>();
            if(before.status!="playing"||!Adjacent(a,b,before.board.Length)||(String.IsNullOrEmpty(before.board[a])&&String.IsNullOrEmpty(before.board[b])))return false;
            string temp=state.board[a];state.board[a]=state.board[b];state.board[b]=temp;state.moves++;
            events.Add(new SwapEvent {type="swap",a=a,b=b,view=state.Clone()});
            int row,index;while(Ready(state,level,out row,out index)) {
                var items=level.sets[index].items;var completed=new List<int>(state.completedSets){index};state.completedSets=completed.ToArray();
                var delivered=new List<string>(state.delivered);delivered.AddRange(items);state.delivered=delivered.ToArray();state.stage=completed.Count;
                for(int c=0;c<3;c++)state.board[row*3+c]=null;
                events.Add(new SwapEvent {type="deliver",row=row,setIndex=index,items=(string[])items.Clone(),view=state.Clone()});
                if(state.stage==level.sets.Length) {state.status="won";break;}
                for(int c=0;c<3;c++) {
                    var survivors=new List<string>();for(int r=0;r<level.rows;r++)if(!String.IsNullOrEmpty(state.board[r*3+c]))survivors.Add(state.board[r*3+c]);
                    for(int r=0;r<level.rows;r++)state.board[r*3+c]=null;
                    for(int k=0;k<survivors.Count;k++)state.board[(level.rows-survivors.Count+k)*3+c]=survivors[k];
                }
                var spawned=new List<string>();if(state.spawnIndex<level.refills.Length) {
                    var refill=level.refills[state.spawnIndex++].items;
                    for(int c=0;c<3;c++)for(int r=0;r<level.rows;r++)if(String.IsNullOrEmpty(state.board[r*3+c])) {state.board[r*3+c]=refill[c];spawned.Add(refill[c]);break;}
                }
                events.Add(new SwapEvent {type="fall",spawned=spawned.ToArray(),view=state.Clone()});
            }
            if(state.status!="won"&&level.moveLimit>0&&state.moves>=level.moveLimit+state.extraMoves)state.status="lost";
            ValidateInventory(state,level);return true;
        }
        public static void ValidateInventory(SwapState state,LevelData level)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);int count=0;
            Action<string> add=item=>{if(!String.IsNullOrEmpty(item)){count++;if(!seen.Add(item))throw new InvalidOperationException("Repeated item: "+item);}};
            foreach(var item in state.board)add(item);foreach(var item in state.delivered)add(item);
            for(int i=state.spawnIndex;i<level.refills.Length;i++)foreach(var item in level.refills[i].items)add(item);
            if(count!=level.items.Length||state.delivered.Length!=state.stage*3||state.completedSets.Length!=state.stage)throw new InvalidOperationException("Invalid progress inventory.");
            foreach(var item in level.items)if(!seen.Contains(item.id))throw new InvalidOperationException("Missing item: "+item.id);
        }
        // Exact distance to the next requested trio. This is not a global level optimum.
        // Recompute from the actual board; referenceSolution is for verification only.
        public static bool TryHint(SwapState state,LevelData level,int preferred,out SwapData move)
        {
            move=null;if(state.status!="playing")return false;
            var table=GetTable(level.rows);var eligible=Eligible(state,level);if(eligible.Contains(preferred))eligible=new List<int>{preferred};
            int best=Int32.MaxValue;
            foreach(int index in eligible) {
                var goal=level.sets[index].items;int p0=Array.IndexOf(state.board,goal[0]),p1=Array.IndexOf(state.board,goal[1]),p2=Array.IndexOf(state.board,goal[2]);
                if(p0<0||p1<0||p2<0)throw new InvalidOperationException("Requested item unavailable.");
                int code=table.Code(p0,p1,p2),distance=table.distance[code];
                if(distance==0)continue;
                foreach(var edge in table.edges) {
                    int next=table.Code(Moved(p0,edge.a,edge.b),Moved(p1,edge.a,edge.b),Moved(p2,edge.a,edge.b));
                    if(table.distance[next]==distance-1&&table.targetRow[next]==table.targetRow[code]) {
                        if(distance<best) {move=new SwapData{a=edge.a,b=edge.b};best=distance;}break;
                    }
                }
            }return move!=null;
        }
        static int Moved(int p,int a,int b) {return p==a?b:p==b?a:p;}
        sealed class Table
        {
            public int size;public int[] distance,targetRow;public List<SwapData> edges=new List<SwapData>();
            public int Code(int a,int b,int c) {return a+size*b+size*size*c;}
        }
        static readonly Dictionary<int,Table> tables=new Dictionary<int,Table>();
        static Table GetTable(int rows)
        {
            Table table;if(tables.TryGetValue(rows,out table))return table;
            table=new Table{size=rows*3};int n=table.size;table.distance=new int[n*n*n];table.targetRow=new int[n*n*n];
            for(int i=0;i<table.distance.Length;i++)table.distance[i]=table.targetRow[i]=-1;
            for(int r=0;r<rows;r++)for(int c=0;c<3;c++) {int a=r*3+c;if(c<2)table.edges.Add(new SwapData{a=a,b=a+1});if(r<rows-1)table.edges.Add(new SwapData{a=a,b=a+3});}
            // Match the reference planner's horizontal-then-vertical tie breaking.
            table.edges.Sort((a,b)=> {int av=a.b-a.a==1?0:1,bv=b.b-b.a==1?0:1;return av!=bv?av.CompareTo(bv):a.a.CompareTo(b.a);});
            var queue=new Queue<int>();for(int r=0;r<rows;r++) {int code=table.Code(r*3,r*3+1,r*3+2);table.distance[code]=0;table.targetRow[code]=r;queue.Enqueue(code);}
            while(queue.Count>0) {
                int code=queue.Dequeue(),a=code%n,b=code/n%n,c=code/n/n;
                foreach(var edge in table.edges) {int next=table.Code(Moved(a,edge.a,edge.b),Moved(b,edge.a,edge.b),Moved(c,edge.a,edge.b));if(table.distance[next]>=0)continue;table.distance[next]=table.distance[code]+1;table.targetRow[next]=table.targetRow[code];queue.Enqueue(next);}
            }tables.Add(rows,table);return table;
        }
    }
}
