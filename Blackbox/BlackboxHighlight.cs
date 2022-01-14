using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class BlackboxHighlight
  {
    public int blackboxId;
    public List<int> warningIds = new List<int>();

    public const int blackboxSignalId = 60001;

    public void RequestHighlight(Blackbox blackbox)
    {
      if (blackbox == null)
      {
        ClearHighlight();
        return;
      }

      if (blackboxId == blackbox.Id)
        return;

      if (blackboxId > 0 && blackboxId != blackbox.Id)
        ClearHighlight();

      blackboxId = blackbox.Id;
      DoHighlight(blackbox);
    }

    public void DoHighlight(Blackbox blackbox)
    {
      var entityIds = blackbox.Selection.entityIds;
      var factoryId = blackbox.Selection.factoryIndex;

      var warningSystem = GameMain.data.warningSystem;
      foreach (var entityId in entityIds)
      {
        var warningId = warningSystem.NewWarningData(factoryId, entityId, blackboxSignalId);
        warningIds.Add(warningId);
      }
    }

    public void StopHighlight()
    {
      var warningSystem = GameMain.data.warningSystem;
      foreach (var warningId in warningIds)
        warningSystem.RemoveWarningData(warningId);
      warningIds.Clear();
    }

    public void ClearHighlight()
    {
      StopHighlight();
      blackboxId = 0;
    }

    const int saveLogicVersion = 1;

    public void PreserveVanillaSaveBefore()
    {
      StopHighlight();
    }

    public void PreserveVanillaSaveAfter()
    {
      if (blackboxId > 0)
      {
        var blackbox = BlackboxManager.Instance.blackboxes.Find(x => x.Id == blackboxId);
        DoHighlight(blackbox);
      }
    }

    public void Export(BinaryWriter w)
    {
      w.Write(saveLogicVersion);
      w.Write(blackboxId);
    }

    public void Import(BinaryReader r)
    {
      var saveLogicVersion = r.ReadInt32();
      blackboxId = r.ReadInt32();
      PreserveVanillaSaveAfter();
    }
  }
}