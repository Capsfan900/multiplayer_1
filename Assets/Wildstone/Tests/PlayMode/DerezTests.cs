using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // D028 elimination read: the soldier derezzes into team voxels, then hides; respawn restores the body.
    [TestFixture,Category("Native")]
    public sealed class DerezTests
    {
        PresentationCatalog catalog;
        PlayerVisual visual;
        ArenaVfx vfx;
        CombatState player;
        [SetUp] public void Setup()
        {
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            vfx=new GameObject("Derez vfx",typeof(ArenaVfx)).GetComponent<ArenaVfx>(); vfx.Build(catalog);
            visual=new GameObject("Derez visual",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
            visual.Initialize(catalog,1); visual.Vfx=vfx;
            player=new CombatState { Participant=2,LifeGeneration=1,Alive=true,Connected=true };
            Present();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(visual.gameObject); Object.DestroyImmediate(vfx.gameObject); }
        void Present()
        {
            visual.Present(player,default,catalog);
            visual.PresentHandling(player,10,.016f,false);
        }
        static int Pixels(ArenaVfx vfx) => vfx.transform.Find("Vfx Pixels").GetComponent<ParticleSystem>().particleCount;

        [UnityTest] public IEnumerator EliminationDissolvesIntoTeamVoxelsThenHidesAndRespawnRestoresTheBody()
        {
            var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            Assert.That(renderers.Length,Is.GreaterThan(0));
            var original=renderers[0].sharedMaterial;
            Assert.That(renderers[0].gameObject.layer,Is.EqualTo(PlayerVisual.OutlineLayer),"Living soldiers are outlined.");
            player.Alive=false; Present();
            Assert.That(visual.gameObject.activeSelf,Is.True,"The body stays for the derez.");
            float start=Time.realtimeSinceStartup; bool midway=false; int peak=0;
            while(Time.realtimeSinceStartup-start<PlayerVisual.DerezDelay+PlayerVisual.DerezSeconds+.3f)
            {
                Present();
                if(visual.Derezzing && visual.DerezProgress>.3f && visual.DerezProgress<.9f)
                {
                    midway=true;
                    Assert.That(renderers[0].sharedMaterial.shader.name,Is.EqualTo("Wildstone/Derez"));
                    Assert.That(renderers[0].gameObject.layer,Is.Not.EqualTo(PlayerVisual.OutlineLayer),"The outline cannot rim voxels that are gone.");
                }
                peak=Mathf.Max(peak,Pixels(vfx));
                yield return null;
            }
            Assert.That(midway,Is.True,"The dissolve was observed in progress.");
            Assert.That(peak,Is.GreaterThan(20),"Voxels burst from the body.");
            Assert.That(visual.gameObject.activeSelf,Is.False,"The body hides once the derez completes.");
            player.Alive=true; player.LifeGeneration=2; Present();
            Assert.That(visual.gameObject.activeSelf,Is.True);
            Assert.That(visual.Derezzing,Is.False);
            Assert.That(renderers[0].sharedMaterial,Is.EqualTo(original),"Respawn restores the armour materials.");
            Assert.That(renderers[0].gameObject.activeInHierarchy,Is.True);
            Assert.That(renderers[0].gameObject.layer,Is.EqualTo(PlayerVisual.OutlineLayer),"Respawn restores the outline.");
        }
        // A confirmed hit lights the body for a moment, a headshot brighter, and the flash clears itself.
        [Test] public void ConfirmedHitFlashesTheBodyThenClears()
        {
            var renderer=visual.GetComponentsInChildren<SkinnedMeshRenderer>(false)[0];
            Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"),Is.True,"Armour can carry the flash.");
            float Emission() { var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block.isEmpty?0:block.GetColor("_EmissionColor").maxColorComponent; }
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            visual.React(new CombatEvent(CombatEventKind.Hit,action,2,position:Vector3.up,amount:10));
            float body=Emission();
            Assert.That(body,Is.GreaterThan(1));
            visual.React(new CombatEvent(CombatEventKind.Hit,action,2,position:Vector3.up,amount:10,headshot:true));
            Assert.That(Emission(),Is.GreaterThan(body),"Headshots flash brighter.");
            visual.PresentHandling(player,10,PlayerVisual.HitFlashSeconds*.5f,false);
            Assert.That(Emission(),Is.GreaterThan(0).And.LessThan(body*2));
            visual.PresentHandling(player,10,PlayerVisual.HitFlashSeconds,false);
            Assert.That(visual.HitFlash,Is.Zero);
            Assert.That(Emission(),Is.Zero,"The flash clears its override.");
        }
        // A soldier must never render in its bind (T) pose: not on its first presented frame, nor right after a respawn.
        [Test] public void FirstFrameAndRespawnAreAlreadyPosed()
        {
            Object.DestroyImmediate(visual.gameObject);
            visual=new GameObject("Fresh visual",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
            visual.Initialize(catalog,1); visual.Vfx=vfx;
            var animator=visual.GetComponentInChildren<Animator>(true);
            float ArmDrop() => animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y-animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y;
            float bind=ArmDrop();
            Present();
            Assert.That(ArmDrop(),Is.GreaterThan(bind+.1f),"First frame: the arm hangs or holds, it is not straight out.");
            player.Alive=false; Present();
            player.Alive=true; player.LifeGeneration=2; Present();
            Assert.That(ArmDrop(),Is.GreaterThan(bind+.1f),"Respawn frame: Rebind must not leave the bind pose on screen.");
        }
        [Test] public void DisconnectedEliminationHidesImmediately()
        {
            player.Alive=false; player.Connected=false; Present();
            Assert.That(visual.gameObject.activeSelf,Is.False);
            Assert.That(Pixels(vfx),Is.EqualTo(0));
        }
    }
}
