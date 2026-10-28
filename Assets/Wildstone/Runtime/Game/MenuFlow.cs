using System;
namespace Wildstone.Game
{
    public enum MenuScreen { Title, Online, Practice, Loading, Playing, Pause, Settings, Results, Failure }
    public sealed class MenuFlow
    {
        public MenuScreen Screen { get; private set; }=MenuScreen.Title;
        public int Generation { get; private set; }
        public bool GameplayInput => Screen==MenuScreen.Playing;
        public bool ForegroundLoading { get; private set; }
        public void Show(MenuScreen screen) { Screen=screen; }
        public int BeginLoad() { ForegroundLoading=true; Screen=MenuScreen.Loading; return ++Generation; }
        public void InvalidateLoad() { Generation++; ForegroundLoading=false; }
        public void CancelLoad() { InvalidateLoad(); Screen=MenuScreen.Title; }
        public bool CompleteLoad(int generation,bool practice)
        {
            if(generation!=Generation) return false;
            if(ForegroundLoading) Screen=practice?MenuScreen.Playing:MenuScreen.Title;
            ForegroundLoading=false; return true;
        }
        public bool FailLoad(int generation)
        {
            if(generation!=Generation || !ForegroundLoading) return false;
            ForegroundLoading=false; Screen=MenuScreen.Failure; return true;
        }
        public void LoseFocus() { if(Screen==MenuScreen.Playing) Screen=MenuScreen.Pause; }
        public static bool ValidJoinCode(string code)
        {
            if(string.IsNullOrWhiteSpace(code)) return false;
            code=code.Trim(); if(code.Length<4 || code.Length>12) return false;
            foreach(char c in code) if(!((c>='A'&&c<='Z')||(c>='a'&&c<='z')||(c>='0'&&c<='9'))) return false;
            return true;
        }
    }
}
