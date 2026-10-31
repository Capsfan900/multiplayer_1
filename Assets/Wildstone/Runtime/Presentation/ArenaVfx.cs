using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wildstone.Presentation
{
    // D028 pooled effect primitives adapted from vibegame1 (SparkBurstFx, ImpactBlast, ShockwaveFx, SlideFx):
    // one emit-only particle system per look, a capped light pool, shockwave quads and projected scorch marks
    // (blast scorches on the ground plus a separate ring of short laser burns on any surface).
    // Everything is allocated in Build, so a saturated fight reuses the same objects; nothing has a collider.
    public sealed class ArenaVfx : MonoBehaviour
    {
        public const int LightCapacity=4, WaveCapacity=12, ScorchCapacity=12, BurnCapacity=24, MarkCapacity=ScorchCapacity+BurnCapacity;
        ParticleSystem sparks,glow,smoke,fire,dust,debris,motes,pixels;
        readonly Light[] lights=new Light[LightCapacity];
        readonly float[] lightStart=new float[LightCapacity],lightEnd=new float[LightCapacity],lightPeak=new float[LightCapacity];
        readonly Transform[] waves=new Transform[WaveCapacity];
        readonly MeshRenderer[] waveRenderers=new MeshRenderer[WaveCapacity];
        readonly float[] waveStart=new float[WaveCapacity],waveEnd=new float[WaveCapacity],waveRadius=new float[WaveCapacity];
        readonly Color[] waveColors=new Color[WaveCapacity];
        readonly Transform[] scorches=new Transform[MarkCapacity];
        readonly MeshRenderer[] scorchRenderers=new MeshRenderer[MarkCapacity];
        readonly float[] scorchStart=new float[MarkCapacity],scorchLife=new float[MarkCapacity];
        int lightIndex,waveIndex,scorchIndex,burnIndex;
        MaterialPropertyBlock block;
        Mesh quad;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Opacity=Shader.PropertyToID("_Opacity");
        public const float ScorchSeconds=14, BurnSeconds=6;

        public void Build(PresentationCatalog catalog)
        {
            block=new MaterialPropertyBlock();
            sparks=System("Sparks",catalog.VfxSpark,384,true,1.6f);
            glow=System("Glow",catalog.VfxGlow,96,false,0);
            smoke=System("Smoke",catalog.VfxSmoke,96,false,-.05f);
            fire=System("Fire",catalog.VfxFire,48,false,-.1f);
            dust=System("Dust",catalog.VfxDust,128,false,.05f);
            debris=System("Debris",catalog.VfxDebris,96,false,2.2f);
            motes=System("Motes",catalog.VfxGlow,96,false,-.02f);
            pixels=System("Pixels",catalog.VfxPixel,768,false,.45f);
            quad=new Mesh { name="Vfx wave" };
            quad.vertices=new[] { new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f) };
            quad.uv=new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            quad.triangles=new[] { 0,1,2, 0,2,3 };
            quad.RecalculateBounds();
            for(int i=0;i<LightCapacity;i++)
            {
                var go=new GameObject("Vfx light "+i,typeof(Light));
                go.transform.SetParent(transform,false);
                var light=go.GetComponent<Light>();
                light.type=LightType.Point; light.shadows=LightShadows.None; light.renderMode=LightRenderMode.ForcePixel;
                light.enabled=false; lights[i]=light;
            }
            for(int i=0;i<WaveCapacity;i++)
            {
                var go=new GameObject("Vfx wave "+i,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=quad;
                waveRenderers[i]=Quiet(go.GetComponent<MeshRenderer>(),catalog.VfxShockwave);
                waves[i]=go.transform; go.SetActive(false);
            }
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for(int i=0;i<MarkCapacity;i++)
            {
                var go=new GameObject("Scorch "+i,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=cube;
                scorchRenderers[i]=Quiet(go.GetComponent<MeshRenderer>(),catalog.Scorch);
                scorches[i]=go.transform; go.SetActive(false);
            }
        }
        static MeshRenderer Quiet(MeshRenderer renderer,Material material)
        {
            if(material==null) throw new System.ArgumentException("Missing effect material on "+renderer.name);
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return renderer;
        }
        ParticleSystem System(string name,Material material,int capacity,bool stretched,float gravity)
        {
            if(material==null) throw new System.ArgumentException("Missing effect material: "+name);
            var go=new GameObject("Vfx "+name,typeof(ParticleSystem));
            go.transform.SetParent(transform,false);
            var system=go.GetComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;
            main.playOnAwake=false; main.loop=true; main.maxParticles=capacity; main.useUnscaledTime=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.gravityModifier=gravity; main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=system.emission; emission.enabled=false;
            var shape=system.shape; shape.enabled=false;
            var fade=system.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(1,0),new GradientAlphaKey(.85f,.35f),new GradientAlphaKey(0,1) });
            fade.color=gradient;
            if(!stretched)
            {
                var size=system.sizeOverLifetime; size.enabled=true;
                size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.55f,1,1.25f));
                var rotation=system.rotationOverLifetime; rotation.enabled=name=="Smoke" || name=="Debris" || name=="Dust";
                rotation.z=new ParticleSystem.MinMaxCurve(-1.2f,1.2f);
            }
            var drag=system.limitVelocityOverLifetime; drag.enabled=name!="Debris" && name!="Sparks"; drag.drag=name=="Pixels"?1.6f:2.5f; drag.limit=100;
            var tiles=material.HasProperty("_FlipbookTiles")?material.GetVector("_FlipbookTiles"):Vector4.one;
            var renderer=go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.renderMode=stretched?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            renderer.velocityScale=stretched?.035f:0; renderer.lengthScale=stretched?1.2f:1;
            renderer.sortingFudge=name=="Smoke" || name=="Dust"?10:0;
            if(name=="Pixels")
            {
                // Derez voxels: tumbling additive cubes that hold their size, then wink out.
                renderer.renderMode=ParticleSystemRenderMode.Mesh;
                renderer.mesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                renderer.alignment=ParticleSystemRenderSpace.World;
                main.startRotation3D=true;
                var size=system.sizeOverLifetime;
                size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.65f,.9f),new Keyframe(1,0)));
                var spin=system.rotationOverLifetime; spin.enabled=true; spin.separateAxes=true;
                spin.x=new ParticleSystem.MinMaxCurve(-6,6); spin.y=new ParticleSystem.MinMaxCurve(-6,6); spin.z=new ParticleSystem.MinMaxCurve(-6,6);
            }
            if(tiles.x*tiles.y>1)
            {
                var sheet=system.textureSheetAnimation; sheet.enabled=true;
                sheet.numTilesX=Mathf.Max(1,(int)tiles.x); sheet.numTilesY=Mathf.Max(1,(int)tiles.y);
                bool random=name=="Debris";
                if(tiles.z>0) { sheet.animation=ParticleSystemAnimationType.SingleRow; sheet.rowMode=ParticleSystemAnimationRowMode.Random; }
                sheet.frameOverTime=random?new ParticleSystem.MinMaxCurve(0):new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                sheet.startFrame=random?new ParticleSystem.MinMaxCurve(0,sheet.numTilesX*sheet.numTilesY):new ParticleSystem.MinMaxCurve(0);
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                    ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.AnimBlend });
            }
            system.Play();
            return system;
        }
        static void Emit(ParticleSystem system,Vector3 position,Vector3 velocity,float size,float life,Color color,float rotation=0)
        {
            var p=new ParticleSystem.EmitParams { position=position,velocity=velocity,startSize=size,startLifetime=life,startColor=color,rotation=rotation,applyShapeToPosition=false };
            system.Emit(p,1);
        }
        // Deterministic per call site so captures repeat; presentation never feeds simulation.
        static Vector3 Spread(Vector3 forward,float cone,int i,int count,float seed)
        {
            forward=forward.sqrMagnitude>.0001f?forward.normalized:Vector3.up;
            Vector3 side=Vector3.Cross(forward,Mathf.Abs(forward.y)<.9f?Vector3.up:Vector3.right).normalized,up=Vector3.Cross(forward,side);
            float angle=(i+.5f)/count*Mathf.PI*2+seed*7.31f, tilt=cone*(.35f+.65f*Mathf.Repeat(i*.618f+seed,1));
            return (forward+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*tilt).normalized;
        }

        public void SparkBurst(Vector3 position,Vector3 forward,Color color,int count,float speed,float cone=.9f)
        {
            float seed=Mathf.Repeat(position.x*.37f+position.z*.11f,1);
            for(int i=0;i<count;i++)
            {
                float s=speed*(.55f+.45f*Mathf.Repeat(i*.37f+seed,1));
                Emit(sparks,position,Spread(forward,cone,i,count,seed)*s,.025f+.02f*Mathf.Repeat(i*.71f,1),.22f+.2f*Mathf.Repeat(i*.53f+seed,1),color);
            }
        }
        public void Flash(Vector3 position,Color color,float size,float life=.08f) => Emit(glow,position,Vector3.zero,size,life,color);
        public void Pixel(Vector3 position,Vector3 velocity,float size,float life,Color color,float spin)
        {
            var p=new ParticleSystem.EmitParams { position=position,velocity=velocity,startSize=size,startLifetime=life,startColor=color,
                rotation3D=new Vector3(spin,spin*1.7f,spin*.6f),applyShapeToPosition=false };
            pixels.Emit(p,1);
        }
        public void Lamp(Vector3 position,Color color,float intensity,float range,float seconds)
        {
            int slot=lightIndex++%LightCapacity;
            var light=lights[slot];
            light.transform.position=position; light.color=color;
            light.range=Mathf.Min(range,8); lightPeak[slot]=Mathf.Min(intensity,6);
            light.intensity=lightPeak[slot]; light.enabled=true;
            lightStart[slot]=Time.unscaledTime; lightEnd[slot]=lightStart[slot]+Mathf.Min(seconds,.25f);
        }
        public void Wave(Vector3 position,Vector3 normal,float radius,Color color,float seconds)
        {
            int slot=waveIndex++%WaveCapacity;
            var wave=waves[slot]; wave.gameObject.SetActive(true);
            wave.SetPositionAndRotation(position+normal.normalized*.04f,Quaternion.FromToRotation(Vector3.up,normal.sqrMagnitude>.0001f?normal:Vector3.up));
            wave.localScale=Vector3.one*radius*.3f;
            waveStart[slot]=Time.unscaledTime; waveEnd[slot]=waveStart[slot]+seconds; waveRadius[slot]=radius; waveColors[slot]=color;
        }
        public void Scorch(Vector3 position,float radius)
        {
            if(!Physics.Raycast(position+Vector3.up*.6f,Vector3.down,out var ground,2.5f,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) return;
            Mark(scorchIndex++%ScorchCapacity,ground.point,Vector3.up,radius,1.2f,ScorchSeconds);
        }
        // A laser burn on the struck surface, projected along its normal; burns never evict blast scorches.
        public void Burn(Vector3 point,Vector3 normal,float radius) => Mark(ScorchCapacity+burnIndex++%BurnCapacity,point,normal,radius,.3f,BurnSeconds);
        void Mark(int slot,Vector3 point,Vector3 normal,float radius,float depth,float seconds)
        {
            var mark=scorches[slot]; mark.gameObject.SetActive(true);
            mark.SetPositionAndRotation(point,Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,Mathf.Repeat(point.x*57+point.y*13+point.z*31,360),0));
            mark.localScale=new Vector3(radius*2,depth,radius*2);
            scorchStart[slot]=Time.unscaledTime; scorchLife[slot]=seconds;
        }
        public void Smoke(Vector3 position,float radius,int count,Color color)
        {
            for(int i=0;i<count;i++)
            {
                var direction=Spread(Vector3.up,1.4f,i,count,.3f);
                Emit(smoke,position+direction*radius*.35f,direction*radius*.9f+Vector3.up*.6f,radius*(.9f+.5f*Mathf.Repeat(i*.43f,1)),1.6f+.8f*Mathf.Repeat(i*.61f,1),color,i*1.3f);
            }
        }
        public void Fireball(Vector3 position,float radius,int count,Color color)
        {
            for(int i=0;i<count;i++)
            {
                var direction=Spread(Vector3.up,1.8f,i,count,.7f);
                Emit(fire,position+direction*radius*.2f,direction*radius*2.2f,radius*(.8f+.4f*Mathf.Repeat(i*.29f,1)),.38f+.2f*Mathf.Repeat(i*.47f,1),color,i*2.1f);
            }
        }
        public void Dust(Vector3 position,Vector3 velocity,float size,int count,Color color)
        {
            for(int i=0;i<count;i++)
            {
                var direction=Spread(Vector3.up,2.6f,i,count,.1f);
                Emit(dust,position+direction*size*.3f,velocity+new Vector3(direction.x,Mathf.Abs(direction.y)*.4f,direction.z)*size*1.6f,size*(.7f+.5f*Mathf.Repeat(i*.31f,1)),.6f+.5f*Mathf.Repeat(i*.57f,1),color,i);
            }
        }
        public void Debris(Vector3 position,float speed,int count,Color color)
        {
            for(int i=0;i<count;i++)
                Emit(debris,position+Vector3.up*.1f,Spread(Vector3.up,1.3f,i,count,.9f)*speed*(.5f+.5f*Mathf.Repeat(i*.77f,1)),.06f+.07f*Mathf.Repeat(i*.39f,1),.7f+.5f*Mathf.Repeat(i*.21f,1),color,i);
        }
        public void Mote(Vector3 position,Color color)
        {
            Emit(motes,position,Vector3.up*(.5f+.4f*Mathf.Repeat(position.x*3.1f,1)),.07f,2.2f,color);
        }
        public void Clear()
        {
            foreach(var system in new[] { sparks,glow,smoke,fire,dust,debris,motes,pixels }) if(system!=null) system.Clear();
            for(int i=0;i<LightCapacity;i++) if(lights[i]!=null) lights[i].enabled=false;
            for(int i=0;i<WaveCapacity;i++) if(waves[i]!=null) waves[i].gameObject.SetActive(false);
            for(int i=0;i<MarkCapacity;i++) if(scorches[i]!=null) scorches[i].gameObject.SetActive(false);
        }
        public void Animate(float now)
        {
            for(int i=0;i<LightCapacity;i++)
            {
                var light=lights[i];
                if(light==null || !light.enabled) continue;
                float age=(now-lightStart[i])/Mathf.Max(.001f,lightEnd[i]-lightStart[i]);
                if(age>=1) { light.enabled=false; continue; }
                light.intensity=lightPeak[i]*(1-age)*(1-age);
            }
            for(int i=0;i<WaveCapacity;i++)
            {
                if(waves[i]==null || !waves[i].gameObject.activeSelf) continue;
                float age=(now-waveStart[i])/Mathf.Max(.001f,waveEnd[i]-waveStart[i]);
                if(age>=1) { waves[i].gameObject.SetActive(false); continue; }
                float eased=1-(1-age)*(1-age)*(1-age);
                waves[i].localScale=Vector3.one*waveRadius[i]*2*Mathf.Lerp(.15f,1,eased);
                block.SetColor(BaseColor,waveColors[i]*(1-age));
                waveRenderers[i].SetPropertyBlock(block);
            }
            for(int i=0;i<MarkCapacity;i++)
            {
                if(scorches[i]==null || !scorches[i].gameObject.activeSelf) continue;
                float age=(now-scorchStart[i])/scorchLife[i];
                if(age>=1) { scorches[i].gameObject.SetActive(false); continue; }
                block.Clear(); block.SetFloat(Opacity,Mathf.Clamp01((1-age)*3));
                scorchRenderers[i].SetPropertyBlock(block);
                block.Clear();
            }
        }
        void OnDestroy() { if(quad!=null) Destroy(quad); }
    }
}
