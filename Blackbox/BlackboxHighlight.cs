using System;
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

      var entityIds = blackbox.Selection.entityIds;
      var factoryId = blackbox.Selection.factoryIndex;

      var warningSystem = GameMain.data.warningSystem;
      foreach (var entityId in entityIds)
      {
        var warningId = warningSystem.NewWarningData(factoryId, entityId, blackboxSignalId);
        warningIds.Add(warningId);
      }
      blackboxId = blackbox.Id;
    }

    public void ClearHighlight()
    {
      var warningSystem = GameMain.data.warningSystem;
      foreach (var warningId in warningIds)
        warningSystem.RemoveWarningData(warningId);
      warningIds.Clear();
      blackboxId = 0;
    }
  }
}