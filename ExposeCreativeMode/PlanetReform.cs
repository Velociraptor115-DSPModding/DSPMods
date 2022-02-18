namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public static class PlanetReform
  {
    public static void SetPlanetModLevel(PlanetFactory factory, bool bury, int modLevel)
    {
      var planet = factory.planet;
      if (planet == null || planet.type == EPlanetType.Gas)
        return;
      
      var modData = planet.data.modData;
      for (int i = 0; i < modData.Length; i++)
        modData[i] = (byte)((modLevel & 3) | ((modLevel & 3) << 4));
      
      var dirtyFlags = planet.dirtyFlags;
      for (int i = 0; i < dirtyFlags.Length; i++)
        dirtyFlags[i] = true;
      planet.landPercentDirty = true;
      
      if (planet.UpdateDirtyMeshes())
        factory.RenderLocalPlanetHeightmap();
      
      var vegePool = factory.vegePool;
      float groundLevel = planet.realRadius + 0.2f;
      var isFlattened = (modLevel & 3) == 3;
      for (int n = 1; n < factory.vegeCursor; n++)
      {
        var currentPos = vegePool[n].pos;
        var vegeGroundLevel =
          isFlattened ?
            groundLevel :
            planet.data.QueryModifiedHeight(currentPos) - 0.13f;
        vegePool[n].pos = currentPos.normalized * vegeGroundLevel;
        GameMain.gpuiManager.AlterModel((int)vegePool[n].modelIndex, vegePool[n].modelId, n, vegePool[n].pos,
          vegePool[n].rot, false);
      }
      
      ModifyAllVeinsHeight(factory, bury);
    }

    public static void ModifyAllVeinsHeight(PlanetFactory factory, bool bury)
    {
      var planet = factory.planet;
      var physics = planet.physics;
      var veinPool = factory.veinPool;
      for (int i = 1; i < factory.veinCursor; i++)
      {
        var veinPoolPos = veinPool[i].pos;
        var veinColliderId = veinPool[i].colliderId;
        var heightToSet = bury ? planet.realRadius - 50f : planet.data.QueryModifiedHeight(veinPool[i].pos) - 0.13f;
        physics.colChunks[veinColliderId >> 20].colliderPool[veinColliderId & 0xFFFFF].pos = physics.GetColliderData(veinColliderId).pos.normalized * (heightToSet + 0.4f);
        veinPool[i].pos = veinPoolPos.normalized * heightToSet;
        physics.SetPlanetPhysicsColliderDirty();
        GameMain.gpuiManager.AlterModel(veinPool[i].modelIndex, veinPool[i].modelId, i, veinPool[i].pos, false);
      }
      GameMain.gpuiManager.SyncAllGPUBuffer();
    }
  }
}