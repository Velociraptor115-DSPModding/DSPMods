using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DysonSphereProgram.Modding.Blackbox
{
  internal static class PlanetFactorySimulation
  {
    internal static int[] dummyRegister = new int[12000];

    internal static void ExportImport(Action<BinaryWriter> export, Action<BinaryReader> import)
    {
      using (var ms = new MemoryStream())
      {
        using (var w = new BinaryWriter(ms))
        {
          export(w);
          ms.Position = 0;
          using (var r = new BinaryReader(ms))
          {
            import(r);
          }
        }
      }
    }

    public static PlanetFactory CloneForSimulation(PlanetFactory factory)
    {
      var newFactory = new PlanetFactory();
      newFactory.index = -1;
      newFactory.gameData = factory.gameData;
      newFactory.planet = null;
      newFactory.landed = false;

      newFactory.SetEntityCapacity(factory.entityCapacity);
      newFactory.entityCursor = factory.entityCursor;
      newFactory.entityRecycleCursor = factory.entityRecycleCursor;
      factory.entityPool.CopyTo(newFactory.entityPool, 0);
      // The entityConnPool seems to be only required for FactorySystem.Import
      factory.entityConnPool.CopyTo(newFactory.entityConnPool, 0);
      // The entityMutexs is required by Factory.PickFrom and Factory.InsertInto
      // which are in turn used by inserters, power generators and miners, so we'll have to support it
      for (int i = 1; i < newFactory.entityCursor; i++)
      {
        ref var entity = ref newFactory.entityPool[i];
        if (entity.beltId == 0 && entity.inserterId == 0 && entity.splitterId == 0 && entity.monitorId == 0)
          newFactory.entityMutexs[i] = new Mutex(i);
      }

      // Ensure cargoContainer works as expected
      var newCargoContainer = new CargoContainer(true);
      ExportImport(factory.cargoContainer.Export, newCargoContainer.Import);
      newFactory.cargoContainer = newCargoContainer;

      var newCargoTraffic = new CargoTraffic();
      newCargoTraffic.factory = newFactory;
      newCargoTraffic.container = newCargoContainer;
      ExportImport(factory.cargoTraffic.Export, newCargoTraffic.Import);
			newFactory.cargoTraffic = newCargoTraffic;

      var newPowerSystem = new PowerSystem(factory.planet, true);
      newPowerSystem.planet = null;
      newPowerSystem.factory = newFactory;
      //ExportImport(factory.powerSystem.Export, newPowerSystem.Import);
      newPowerSystem.SetConsumerCapacity(factory.powerSystem.consumerCapacity);
      newPowerSystem.consumerCursor = factory.powerSystem.consumerCursor;
      newPowerSystem.consumerRecycleCursor = factory.powerSystem.consumerRecycleCursor;
      factory.powerSystem.consumerPool.CopyTo(newPowerSystem.consumerPool, 0);
      newFactory.powerSystem = newPowerSystem;

      var newFactorySystem = new FactorySystem(factory.planet, true);
      newFactorySystem.planet = null;
      newFactorySystem.factory = newFactory;
      newFactorySystem.traffic = newCargoTraffic;
      newFactorySystem.storage = null; // This isn't even read anywhere
      
      //newFactorySystem.SetMinerCapacity(0);
      //newFactorySystem.SetInserterCapacity(factory.factorySystem.inserterCapacity);
      //newFactorySystem.inserterCursor = factory.factorySystem.inserterCursor;
      //newFactorySystem.inserterRecycleCursor = factory.factorySystem.inserterRecycleCursor;
      //newFactorySystem.SetAssemblerCapacity(256);
      //newFactorySystem.SetFractionateCapacity(32);
      //newFactorySystem.SetEjectorCapacity(32);
      //newFactorySystem.SetSiloCapacity(32);
      //newFactorySystem.SetLabCapacity(256);
      ExportImport(factory.factorySystem.Export, newFactorySystem.Import);
      newFactory.factorySystem = newFactorySystem;

      var newTransport = new PlanetTransport(factory.gameData, factory.planet, true);
      newTransport.gameData = null;
      newTransport.planet = null;
      newTransport.factory = newFactory;
      newTransport.powerSystem = null;
      if (newTransport.droneRenderer != null)
      {
        newTransport.droneRenderer.Destroy();
        newTransport.droneRenderer = null;
      }
      // Can't use ExportImport, since PlanetTransport.Import affects global state
      {
        newTransport.stationCursor = factory.transport.stationCursor;
        newTransport.SetStationCapacity(factory.transport.stationCapacity);
        newTransport.stationRecycleCursor = factory.transport.stationRecycleCursor;
        for (int i = 1; i < factory.transport.stationCursor; i++)
        {
          var station = factory.transport.stationPool[i];
          if (station != null)
          {
            var newStation = new StationComponent();
            ExportImport(station.Export, newStation.Import);
            newTransport.stationPool[i] = newStation;
          }
        }
        factory.transport.stationRecycle.CopyTo(newTransport.stationRecycle, 0);
      }
      newFactory.transport = newTransport;
      
      return newFactory;
		}

    public static void SimulateGameTick(BlackboxBenchmarkV3 benchmark)
    {
      var factory = benchmark.simulationFactory;
      if (factory.factorySystem != null)
      {
        factory.factorySystem.GameTickBeforePower(0, false);
        benchmark.LogPowerConsumer();
        benchmark.LogAssemblerBefore();
        benchmark.LogLabBefore();

        // TODO: Assembler game tick
        //   DONE
        for (int i = 0; i < benchmark.assemblerIds.Length; i++)
        {
          var assemblerId = benchmark.assemblerIds[i];
          ref var assembler = ref factory.factorySystem.assemblerPool[assemblerId];

          // Skipping the customary assembler.id == assemblerId check, since we'd have ideally vetted this beforehand

          if (assembler.recipeId != 0)
          {
            assembler.UpdateNeeds();
            assembler.InternalUpdate(1, dummyRegister, dummyRegister);
            factory.entityNeeds[assembler.entityId] = assembler.needs;
          }
        }

        // TODO: Lab game tick
        //   DONE
        for (int i = 0; i < benchmark.labIds.Length; i++)
        {
          var labId = benchmark.labIds[i];
          ref var lab = ref factory.factorySystem.labPool[labId];

          if (!lab.researchMode && lab.recipeId > 0)
          {
            lab.UpdateNeedsAssemble();
            lab.InternalUpdateAssemble(1, dummyRegister, dummyRegister);
            factory.entityNeeds[lab.entityId] = lab.needs;
          }
        }

        benchmark.LogAssemblerAfter();
        benchmark.LogLabAfter();
        benchmark.LogStationBefore();

        factory.factorySystem.GameTickLabOutputToNext(0, false);

        // TODO: Reset station counts and update needs
        //   DONE
        for (int i = 0; i < benchmark.stationIds.Length; i++)
        {
          var stationId = benchmark.stationIds[i];
          var station = factory.transport.stationPool[stationId];

          for (int j = 0; j < station.storage.Length; j++)
          {
            station.storage[j].count = station.storage[j].max / 2;
          }
          station.UpdateNeeds();
          factory.entityNeeds[station.entityId] = station.needs;
        }

        // TODO: Station input from belt
        // TODO: This makes use of factory.entitySignPool
        //   DONE
        factory.transport.GameTick_InputFromBelt();

        // TODO: Inserter game tick
        // TODO: Check the usage of factory's 'InsertInto' and 'PickFrom'
        //   DONE
        for (int i = 0; i < benchmark.inserterIds.Length; i++)
        {
          var inserterId = benchmark.inserterIds[i];
          ref var inserter = ref factory.factorySystem.inserterPool[inserterId];
          inserter.InternalUpdateNoAnim(factory, factory.entityNeeds, 1);
          if (BlackboxBenchmarkV3.adaptiveStacking)
          {
            DoAdaptiveStacking(ref inserter, factory);
          }
        }

        // TODO: Cargo game tick
        for (int i = 0; i < benchmark.cargoPathIds.Length; i++)
        {
          var cargoPathId = benchmark.cargoPathIds[i];
          var cargoPath = factory.cargoTraffic.pathPool[cargoPathId];
          cargoPath.Update();
        }
        for (int i = 0; i < benchmark.splitterIds.Length; i++)
        {
          var splitterId = benchmark.splitterIds[i];
          ref var splitter = ref factory.cargoTraffic.splitterPool[splitterId];
          splitter.CheckPriorityPreset();
          factory.cargoTraffic
            .UpdateSplitter(
              splitterId,
              splitter.input0, splitter.input1, splitter.input2,
              splitter.output0, splitter.output1, splitter.output2,
              splitter.outFilter
            );
        }

        // TODO: Station output to belt
        // TODO: This makes use of factory.entitySignPool
        factory.transport.GameTick_OutputToBelt();

        benchmark.LogStationAfter();
        benchmark.LogInserter();

        benchmark.EndGameTick();
      }
    }

    public static void DoAdaptiveStacking(ref InserterComponent inserter, PlanetFactory factory)
    {
      if (inserter.careNeeds && inserter.stage == EInserterStage.Picking && inserter.itemId > 0)
      {
        var needs = factory.entityNeeds[inserter.insertTarget];
        var needIdx = 0;

        //inserter.idleTick = 0;

        for (int i = 0; i < needs.Length; i++)
        {
          if (needs[i] == inserter.itemId)
          {
            needIdx = i;
            break;
          }  
        }

        ref var insertEntity = ref factory.entityPool[inserter.insertTarget];
        var max = 1;
        var served = 0;
        if (insertEntity.assemblerId > 0)
        {
          ref var assembler = ref factory.factorySystem.assemblerPool[insertEntity.assemblerId];
          max = assembler.requireCounts[needIdx] * 3;
          served = assembler.served[needIdx];
        }
        else if (insertEntity.labId > 0)
        {
          ref var lab = ref factory.factorySystem.labPool[insertEntity.labId];
          max = 4;
          served = lab.served[needIdx];

          while (lab.nextLabId > 0)
          {
            lab = ref factory.factorySystem.labPool[lab.nextLabId];
            max += 4;
            served += lab.served[needIdx];
          }
        }
        else
        {
          return;
        }

        if (inserter.stackCount >= max - served)
        {
          inserter.time = inserter.speed;
          inserter.stage = EInserterStage.Sending;
        }
      }
    }
  }
}
