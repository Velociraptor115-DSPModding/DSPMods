using System;
using System.Linq;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class AutoBlackbox
  {
    private int currentBlackboxId;
    private int currentFactoryIdx;
    private int currentStationIdx;

    public bool isActive = false;

    private int debounceControl;

    public void GameTick()
    {
      if (!isActive)
        return;

      if (DSPGame.IsMenuDemo)
        return;

      debounceControl = (debounceControl + 1) % 60;

      if (debounceControl != 0)
        return;

      if (currentBlackboxId > 0)
      {
        var blackbox = BlackboxManager.Instance.blackboxes.Find(b => b.Id == currentBlackboxId);
        if (blackbox == null || IsInTerminalState(blackbox.Status))
          currentBlackboxId = 0;
      }

      if (currentBlackboxId == 0)
      {
        var gamedata = GameMain.data;

        currentFactoryIdx = currentFactoryIdx % gamedata.factoryCount;
        
        var factory = gamedata.factories[currentFactoryIdx];

        var blackboxesInFactory =
          from x in BlackboxManager.Instance.blackboxes
          where x.FactoryIndex == currentFactoryIdx
          select x
          ;

        for (int i = 1; i <= factory.transport.stationCursor; i++)
        {
          var effectiveIdx = (i + currentStationIdx) % factory.transport.stationCursor;
          if (effectiveIdx == 0)
            continue;
          if (factory.transport.stationPool[effectiveIdx] != null && factory.transport.stationPool[effectiveIdx].id == effectiveIdx)
          {
            if (!blackboxesInFactory.Any(b => b.Selection.stationIds.Contains(effectiveIdx)))
            {
              Plugin.Log.LogDebug("Found unblackboxed stationIdx: " + effectiveIdx);
              currentStationIdx = effectiveIdx;
              var newBlackbox = BlackboxManager.Instance.CreateForSelection(BlackboxSelection.CreateFrom(factory, new int[] { factory.transport.stationPool[effectiveIdx].entityId }));
              currentBlackboxId = newBlackbox.Id;
              return;
            }
          }
        }

        for (int i = 1; i <= gamedata.factoryCount; i++)
        {
          var effectiveIdx = (i + currentFactoryIdx) % gamedata.factoryCount;
          if (gamedata.factories[effectiveIdx] != null)
          {
            currentFactoryIdx = effectiveIdx;
            return;
          }
        }
      }
    }

    private bool IsInTerminalState(BlackboxStatus status)
    {
      return (
           status == BlackboxStatus.Invalid
        || status == BlackboxStatus.AnalysisFailed
        || status == BlackboxStatus.Blackboxed
      );
    }
  }
}