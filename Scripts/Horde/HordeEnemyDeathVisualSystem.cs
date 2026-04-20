using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public struct HordeEnemyDeathVisual : IComponentData
{
    public const byte BaseColorProperty = 1;
    public const byte ColorProperty = 2;

    public Entity EnemyEntity;
    public Entity RenderEntity;
    public float4 DeadColor;
    public byte ColorPropertyMask;
    public byte IsApplied;
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct HordeEnemyDeathVisualSystem : ISystem
{
    private ComponentLookup<HordeDead> deadLookup;
    private ComponentLookup<URPMaterialPropertyBaseColor> baseColorLookup;
    private ComponentLookup<MaterialColor> colorLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HordeEnemyDeathVisual>();
        deadLookup = state.GetComponentLookup<HordeDead>(true);
        baseColorLookup = state.GetComponentLookup<URPMaterialPropertyBaseColor>(false);
        colorLookup = state.GetComponentLookup<MaterialColor>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        deadLookup.Update(ref state);
        baseColorLookup.Update(ref state);
        colorLookup.Update(ref state);
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var deathVisual in SystemAPI.Query<RefRW<HordeEnemyDeathVisual>>())
        {
            if (deathVisual.ValueRO.IsApplied != 0 || !deadLookup.HasComponent(deathVisual.ValueRO.EnemyEntity))
                continue;

            if ((deathVisual.ValueRO.ColorPropertyMask & HordeEnemyDeathVisual.BaseColorProperty) != 0)
            {
                if (baseColorLookup.HasComponent(deathVisual.ValueRO.RenderEntity))
                {
                    URPMaterialPropertyBaseColor baseColor = baseColorLookup[deathVisual.ValueRO.RenderEntity];
                    baseColor.Value = deathVisual.ValueRO.DeadColor;
                    baseColorLookup[deathVisual.ValueRO.RenderEntity] = baseColor;
                }
                else
                {
                    ecb.AddComponent(deathVisual.ValueRO.RenderEntity, new URPMaterialPropertyBaseColor
                    {
                        Value = deathVisual.ValueRO.DeadColor
                    });
                }
            }

            if ((deathVisual.ValueRO.ColorPropertyMask & HordeEnemyDeathVisual.ColorProperty) != 0)
            {
                if (colorLookup.HasComponent(deathVisual.ValueRO.RenderEntity))
                {
                    MaterialColor color = colorLookup[deathVisual.ValueRO.RenderEntity];
                    color.Value = deathVisual.ValueRO.DeadColor;
                    colorLookup[deathVisual.ValueRO.RenderEntity] = color;
                }
                else
                {
                    ecb.AddComponent(deathVisual.ValueRO.RenderEntity, new MaterialColor
                    {
                        Value = deathVisual.ValueRO.DeadColor
                    });
                }
            }

            deathVisual.ValueRW.IsApplied = 1;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
