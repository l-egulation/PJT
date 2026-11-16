using System;
namespace Deviant.Art {
    // Pure selection helper; no scene or collider changes. Coordinates: X right, Y down.
    public static class Stone32Selection {
        static readonly int[] DX={0,1,1,1,0,-1,-1,-1};
        static readonly int[] DY={-1,-1,0,1,1,1,0,-1};
        public static int Canonical(int mask) {
            for(int i=1;i<8;i+=2)
                if((mask&(1<<((i+7)%8)))==0||(mask&(1<<((i+1)%8)))==0)mask&=~(1<<i);
            return mask;
        }
        public static string Stone(Func<int,int,bool> occupied,int x,int y) {
            int mask=0;
            for(int i=0;i<8;i++)if(occupied(x+DX[i],y+DY[i]))mask|=1<<i;
            int variant=((x*13+y*7)%4+4)%4;
            return "stone-"+Canonical(mask).ToString("X2")+"-v"+variant;
        }
        public static string Platform(int index,int length,bool leftWall=false,bool rightWall=false) {
            if(length<1||index<0||index>=length)throw new ArgumentOutOfRangeException();
            if(length==1)return "single-"+(leftWall?"wall":"free")+"-"+(rightWall?"wall":"free");
            if(index==0)return "left-"+(leftWall?"wall":"free");
            if(index==length-1)return "right-"+(rightWall?"wall":"free");
            return new[]{"middle-a","middle-b","middle-c"}[(index-1)%3];
        }
    }
}
