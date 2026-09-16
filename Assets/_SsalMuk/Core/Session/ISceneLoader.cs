using System.Threading.Tasks;

namespace SsalMuk.Core
{
    public interface ISceneLoader { Task LoadBattleAsync(); Task LoadMainMenuAsync(); }
}
