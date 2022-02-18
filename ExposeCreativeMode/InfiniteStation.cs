using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CommonAPI;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class InfiniteStation
  {
    public bool IsEnabled;

    public void Enable()
    {
      IsEnabled = true;
      Plugin.Log.LogDebug("Infinite Station Enabled");
    }

    public void Disable()
    {
      IsEnabled = false;
      Plugin.Log.LogDebug("Infinite Station Disabled");
    }

    public void Toggle()
    {
      if (!IsEnabled)
        Enable();
      else
        Disable();
    }

    public void GameTick()
    {
      if (!IsEnabled)
        return;
      
      for (int factoryIdx = 0; factoryIdx < GameMain.data.factoryCount; factoryIdx++)
      {
        var transport = GameMain.data.factories[factoryIdx].transport;
        if (transport != null)
        {
          for (int stationIdx = 1; stationIdx < transport.stationCursor; stationIdx++)
          {
            if (transport.stationPool[stationIdx] != null && transport.stationPool[stationIdx].id == stationIdx)
            {
              var ss = transport.stationPool[stationIdx].storage;
              for (int i = 0; i < ss.Length; i++)
              {
                if (ss[i].itemId > 0)
                {
                  var logic = ss[i].remoteLogic == ELogisticStorage.None ? ss[i].localLogic : ss[i].remoteLogic;
                  if (logic == ELogisticStorage.Supply && ss[i].count > ss[i].max / 2)
                  {
                    ss[i].count = ss[i].max / 2;
                  }
                  else if (logic == ELogisticStorage.Demand && ss[i].count < ss[i].max / 2)
                  {
                    ss[i].count = ss[i].max / 2;
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}