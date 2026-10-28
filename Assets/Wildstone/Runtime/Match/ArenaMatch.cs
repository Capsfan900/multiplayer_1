using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;

namespace Wildstone.Match
{
    public enum ArenaMatchPhase { Lobby, Running, Finished, Aborted }
    [Serializable] public sealed class MatchSnapshot
    {
        public string MapId;
        public int MapRevision, CatalogRevision, TickRate, Tick, StartTick, AuthorityGeneration, AbsenceSince, OppositionStableSince, Winner;
        public bool Practice;
        public ArenaMatchPhase Phase;
        public int[] ScoreTicks;
        public CombatState[] Players;
        public PickupState[] Pickups;
        public ArenaProjectile[] Projectiles;
    }
    public sealed class ArenaMatch
    {
        readonly ArenaDefinition map;
        readonly List<CombatState> players=new List<CombatState>(8);
        readonly List<PickupState> pickups=new List<PickupState>();
        readonly List<ArenaProjectile> projectiles=new List<ArenaProjectile>(WeaponCatalog.MaximumProjectiles);
        // At most 128 commands, 64 explosions and eight active swings can emit in one tick.
        readonly List<CombatEvent> events=new List<CombatEvent>(2048);
        readonly CombatAction[] orderedActions=new CombatAction[128], acceptedActions=new CombatAction[128];
        readonly WeaponKind[] acceptedWeapons=new WeaponKind[128];
        readonly ArenaProjectile[] projectileStorage=new ArenaProjectile[WeaponCatalog.MaximumProjectiles];
        readonly ArenaProjectile[] freeProjectiles=new ArenaProjectile[WeaponCatalog.MaximumProjectiles];
        int freeProjectileCount;
        int[] scoreTicks=new int[4];
        int startTick, absenceSince=-1, oppositionStableSince=-1;
        public int Tick { get; private set; }
        public int TickRate { get; }
        public int AuthorityGeneration { get; private set; }=1;
        public bool Practice { get; }
        public ArenaMatchPhase Phase { get; private set; }
        public int Winner { get; private set; }=-1;
        public int Captor { get; private set; }=-1;
        public bool Contested { get; private set; }
        public MovingZone Zone { get; }
        public Vector3 ZonePosition => Zone.Position(Tick-startTick);
        public int ElapsedTicks => Tick-startTick;
        public IReadOnlyList<CombatState> Players => players;
        public IReadOnlyList<PickupState> Pickups => pickups;
        public IReadOnlyList<ArenaProjectile> Projectiles => projectiles;
        public IReadOnlyList<CombatEvent> Events => events;
        public float Score(int team) => scoreTicks[team]/(float)TickRate;
        public ArenaMatch(ArenaDefinition definition,int tickRate=WeaponCatalog.DefaultTickRate,bool practice=false)
        {
            if(definition==null || tickRate<30 || tickRate>240 || definition.Pickups==null || definition.Spawns==null) throw new ArgumentException("Invalid match definition.");
            map=definition; TickRate=tickRate; Practice=practice; Zone=new MovingZone(map.ZoneRoute,tickRate);
            for(int i=0;i<projectileStorage.Length;i++) projectileStorage[i]=new ArenaProjectile();
            ResetProjectilePool();
            for(int i=0;i<map.Pickups.Length;i++)
            {
                if(!ArenaDefinition.KnownItem(map.Pickups[i].Item)) throw new ArgumentException("Unknown pickup.");
                pickups.Add(new PickupState { Id=i, Item=map.Pickups[i].Item, Position=map.Pickups[i].Position });
            }
        }
        public bool AddPlayer(int participant,int team,int connectionGeneration,int lifeGeneration,Vector3 feet)
        {
            if(Phase==ArenaMatchPhase.Finished || Phase==ArenaMatchPhase.Aborted || players.Count>=8 || Find(participant)!=null || players.Count(p=>p.Team==team)>=2) return false;
            var p=new CombatState { Participant=participant, Team=team, ConnectionGeneration=connectionGeneration, LifeGeneration=lifeGeneration, Feet=feet };
            if(!CombatRules.ValidPlayer(p)) return false;
            players.Add(p); players.Sort((a,b)=>a.Participant.CompareTo(b.Participant)); return true;
        }
        public CombatState Find(int participant)
        { for(int i=0;i<players.Count;i++) if(players[i].Participant==participant) return players[i]; return null; }
        public bool Begin()
        {
            if(Phase!=ArenaMatchPhase.Lobby || players.Count==0 || (!Practice && TeamCount()<2)) return false;
            Phase=ArenaMatchPhase.Running; startTick=Tick; return true;
        }
        int TeamCount()
        {
            int mask=0,count=0; for(int i=0;i<players.Count;i++) if(players[i].Connected) mask|=1<<players[i].Team;
            for(;mask!=0;mask&=mask-1) count++; return count;
        }
        public bool SetPose(int participant,int life,Vector3 feet,Vector3 aim,float capsuleHeight=1.8f)
        {
            var p=Find(participant);
            if(p==null || p.LifeGeneration!=life || !CombatRules.Finite(feet) || !CombatRules.Direction(aim) || !CombatRules.Finite(capsuleHeight) || capsuleHeight<.8f || capsuleHeight>1.8f) return false;
            p.Feet=feet; p.Aim=aim; p.CapsuleHeight=capsuleHeight; return true;
        }
        public bool SetConnection(int participant,int generation,bool connected)
        {
            var p=Find(participant);
            if(p==null || generation<p.ConnectionGeneration || (connected && !p.Connected && generation<=p.ConnectionGeneration)) return false;
            if(!connected && generation!=p.ConnectionGeneration) return false;
            if(p.Connected==connected && generation==p.ConnectionGeneration) return true;
            if(connected && p.LifeGeneration>=int.MaxValue-1) { Phase=ArenaMatchPhase.Aborted; return false; }
            p.ConnectionGeneration=generation; p.Connected=connected;
            if(!connected) { p.DisconnectUntil=Tick+5*TickRate; p.Pending=null; }
            else { p.DisconnectUntil=0; Respawn(p); }
            return true;
        }
        public bool Step(int tick,IReadOnlyList<CombatAction> commands,ICombatWorld world,bool authority=true,Action simulateMovement=null)
        {
            if(!authority || Phase!=ArenaMatchPhase.Running || tick!=Tick+1 || tick>int.MaxValue-240000 || world==null || commands==null || commands.Count>128) return false;
            Tick=tick; events.Clear();
            Expire();
            if(Phase!=ArenaMatchPhase.Running) return true;
            int accepted=0;
            for(int i=0;i<commands.Count;i++)
            {
                var action=commands[i]; int j=i;
                while(j>0 && (orderedActions[j-1].Participant>action.Participant ||
                    (orderedActions[j-1].Participant==action.Participant && orderedActions[j-1].Sequence>action.Sequence)))
                { orderedActions[j]=orderedActions[j-1]; j--; }
                orderedActions[j]=action;
            }
            for(int i=0;i<commands.Count;i++)
            {
                if(Reserve(orderedActions[i],world,out var weapon)) { acceptedActions[accepted]=orderedActions[i]; acceptedWeapons[accepted++]=weapon; }
            }
            // The coordinator runs fixed-tick motors here, then SetPose, before combat and occupancy.
            simulateMovement?.Invoke();
            for(int i=0;i<accepted;i++) Resolve(acceptedActions[i],acceptedWeapons[i],world);
            ResolvePending(world);
            AdvanceProjectiles(world);
            foreach(var p in players)
            {
                if(p.Alive && (p.Feet.y<map.KillHeight || (!p.Connected && Tick>=p.DisconnectUntil))) p.Health=0;
                if(p.Alive && p.Health<=0) Kill(p);
                p.Supported=world.TrySupport(p.Feet,out p.SupportPoint,out p.SupportNormal);
            }
            ScoreZone();
            return true;
        }
        CombatAction SystemAction(CombatState p) => new CombatAction { Participant=p.Participant, LifeGeneration=p.LifeGeneration,
            ConnectionGeneration=p.ConnectionGeneration, AuthorityGeneration=AuthorityGeneration, Tick=Tick, Sequence=-1 };
        void Emit(CombatEventKind kind,CombatAction action,int target=0,float amount=0,Vector3 position=default,Vector3 vector=default,string reason=null,int subIndex=0,WeaponKind weapon=WeaponKind.None)
        {
            if(events.Count==events.Capacity) throw new InvalidOperationException("Combat event capacity exceeded.");
            events.Add(new CombatEvent(kind,action,Tick,target,Find(target)?.LifeGeneration??0,position,vector,amount,reason,subIndex,weapon));
        }
        void Expire()
        {
            foreach(var p in players)
            {
                if(p.Shield>0 && Tick>=p.ShieldUntil) { p.Shield=0; Emit(CombatEventKind.ShieldExpired,SystemAction(p),p.Participant); }
                if(p.ReloadUntil>0 && Tick>=p.ReloadUntil) { p.ReloadUntil=0; p.StarterAmmo=12; }
                if(!p.Alive && p.Connected && Tick>=p.RespawnTick) Respawn(p);
            }
        }
        bool Reject(CombatAction a,string reason) { Emit(CombatEventKind.Rejected,a,reason:reason); return false; }
        bool Reserve(CombatAction a,ICombatWorld world,out WeaponKind reservedWeapon)
        {
            reservedWeapon=WeaponKind.None;
            var p=Find(a.Participant);
            if(p==null || a.Tick!=Tick || a.Sequence<0 || a.Sequence>=int.MaxValue-2 || a.Sequence<=p.LastActionSequence ||
                a.ConnectionGeneration!=p.ConnectionGeneration || a.LifeGeneration!=p.LifeGeneration || a.AuthorityGeneration!=AuthorityGeneration ||
                !p.Connected || !p.Alive || a.Kind<CombatActionKind.Fire || a.Kind>CombatActionKind.Pickup) return Reject(a,"Stale, unauthenticated or unavailable avatar.");
            if(!CombatRules.Direction(a.Direction)) return Reject(a,"Invalid aim.");
            p.LastActionSequence=a.Sequence;
            switch(a.Kind)
            {
                case CombatActionKind.SelectStarter: p.SelectedSpecial=false; break;
                case CombatActionKind.SelectSpecial:
                    if(p.SpecialWeapon==WeaponKind.None) return Reject(a,"No special weapon."); p.SelectedSpecial=true; break;
                case CombatActionKind.Reload:
                    if(p.Suppressed(Tick) || p.SelectedSpecial || p.StarterAmmo==12 || p.ReloadUntil>0 || p.Pending!=null) return Reject(a,"Cannot reload.");
                    p.ReloadUntil=Tick+Ticks(WeaponCatalog.ReloadSeconds); break;
                case CombatActionKind.Shield:
                    if(p.ShieldCharges==0 || p.Shield>0) return Reject(a,"Shield unavailable or already active.");
                    p.ShieldCharges--; p.Shield=WeaponCatalog.Shield; p.ShieldUntil=Tick+Ticks(WeaponCatalog.ShieldSeconds);
                    Emit(CombatEventKind.ShieldActivated,a,p.Participant,WeaponCatalog.Shield); break;
                case CombatActionKind.Blink:
                    if(p.Suppressed(Tick) || p.BlinkCharges==0 || !world.TryBlink(p,a.Direction,6,out var landing) ||
                        !CombatRules.Finite(landing) || (landing-p.Feet).sqrMagnitude<.04f || (landing-p.Feet).sqrMagnitude>36.01f || !map.Contains(landing)) return Reject(a,"No legal Blink destination.");
                    p.BlinkCharges--; p.Feet=landing; p.DiscontinuityTick=Tick; p.Pending=null;
                    Emit(CombatEventKind.Blink,a,p.Participant,position:landing); break;
                case CombatActionKind.Pickup:
                    if(!Claim(p,a,world)) return false; break;
                case CombatActionKind.Fire:
                case CombatActionKind.Grenade:
                    bool grenade=a.Kind==CombatActionKind.Grenade;
                    if(p.Suppressed(Tick) || p.Pending!=null || (!grenade && p.ReloadUntil>0) || Tick<(grenade?p.GrenadeReadyTick:p.FireReadyTick)) return Reject(a,"Attack cooldown or control loss.");
                    var weapon=grenade?WeaponKind.Grenade:(p.SelectedSpecial?p.SpecialWeapon:WeaponKind.LineDriver);
                    if(weapon==WeaponKind.None) return Reject(a,"Missing weapon.");
                    var spec=WeaponCatalog.Get(weapon);
                    int ammo=grenade?p.GrenadeAmmo:(p.SelectedSpecial?p.SpecialAmmo:p.StarterAmmo);
                    if(weapon!=WeaponKind.LaserSword && ammo<=0) return Reject(a,"No ammunition.");
                    if(IsProjectile(weapon) && projectiles.Count+reservedProjectiles>=WeaponCatalog.MaximumProjectiles) return Reject(a,"Projectile capacity reached.");
                    if(grenade) { p.GrenadeAmmo--; p.GrenadeReadyTick=Tick+Ticks(spec.Interval); }
                    else
                    {
                        if(weapon!=WeaponKind.LaserSword) { if(p.SelectedSpecial) p.SpecialAmmo--; else p.StarterAmmo--; }
                        p.FireReadyTick=Tick+Ticks(spec.Interval);
                    }
                    if(IsProjectile(weapon)) reservedProjectiles++;
                    reservedWeapon=weapon;
                    break;
            }
            Emit(CombatEventKind.Accepted,a); return true;
        }
        int reservedProjectiles;
        int Ticks(float seconds) => WeaponCatalog.Ticks(seconds,TickRate);
        static bool IsProjectile(WeaponKind kind) => kind==WeaponKind.PulseLobber || kind==WeaponKind.ReboundDisc || kind==WeaponKind.Grenade || kind==WeaponKind.StunGun;
        bool Claim(CombatState p,CombatAction a,ICombatWorld world)
        {
            if(a.PickupId<0 || a.PickupId>=pickups.Count) return Reject(a,"Unknown pickup.");
            var item=pickups[a.PickupId];
            if(item.Generation>=int.MaxValue-1) return Reject(a,"Pickup generation exhausted.");
            if(item.Generation!=a.PickupGeneration || Tick<item.AvailableTick || (item.Position-p.Feet).sqrMagnitude>6.25f || !world.Visible(p.Eye,item.Position)) return Reject(a,"Pickup unavailable or out of reach.");
            var weapon=WeaponCatalog.FromPickup(item.Item);
            if(item.Item=="blink") { if(p.BlinkCharges==1) return Reject(a,"Blink slot full."); p.BlinkCharges=1; }
            else if(item.Item=="shield") { if(p.ShieldCharges==1) return Reject(a,"Shield slot full."); p.ShieldCharges=1; }
            else if(weapon==WeaponKind.Grenade) { if(p.GrenadeAmmo==2) return Reject(a,"Grenades full."); p.GrenadeAmmo=2; }
            else if(weapon==WeaponKind.LineDriver) { if(p.StarterAmmo==12) return Reject(a,"Starter clip full."); p.StarterAmmo=12; p.ReloadUntil=0; }
            else { p.SpecialWeapon=weapon; p.SpecialAmmo=WeaponCatalog.Get(weapon).Ammo; p.SelectedSpecial=true; p.Pending=null; }
            item.Generation++; item.AvailableTick=Tick+Ticks(WeaponCatalog.PickupSeconds); Emit(CombatEventKind.Pickup,a,p.Participant,position:item.Position); return true;
        }
        void Resolve(CombatAction a,WeaponKind weapon,ICombatWorld world)
        {
            if(weapon==WeaponKind.None) return;
            var p=Find(a.Participant); var spec=WeaponCatalog.Get(weapon);
            Emit(CombatEventKind.Shot,a,p.Participant,position:p.Eye,vector:a.Direction,weapon:weapon);
            if(IsProjectile(weapon))
            {
                reservedProjectiles--;
                var shot=freeProjectiles[--freeProjectileCount];
                shot.Action=a; shot.OwnerTeam=p.Team; shot.Weapon=weapon; shot.Position=p.Eye;
                shot.Velocity=a.Direction*spec.Speed+(weapon==WeaponKind.Grenade?Vector3.up*6:Vector3.zero);
                shot.ExpireTick=Tick+Ticks(spec.Lifetime); shot.Bounces=0; shot.HitMask=0; shot.Travel=0;
                projectiles.Add(shot);
            }
            else if(weapon==WeaponKind.LineDriver)
            {
                if(world.Sweep(p.Eye,a.Direction*spec.Range,0,p.Participant,0,out var hit) && hit.Participant!=0)
                    Hit(a,p.Team,Find(hit.Participant),spec.Damage,world,false,hit.Point);
            }
            else
            {
                var pending=p.AttackStorage; pending.Action=a; pending.Weapon=weapon; pending.ResolveTick=Tick+Ticks(spec.Windup);
                pending.EndTick=Tick+Ticks(spec.Windup+spec.Active); pending.Direction=a.Direction; pending.PreviousTip=p.Eye+a.Direction*spec.Range;
                pending.HitMask=0; pending.LastSampleTick=-1; p.Pending=pending;
            }
        }
        void ResolvePending(ICombatWorld world)
        {
            foreach(var p in players)
            {
                var pending=p.Pending;
                if(pending==null || Tick<pending.ResolveTick) continue;
                if(!p.Alive || (Tick<p.StunUntil && pending.Action.Tick<Tick) || p.DiscontinuityTick>pending.Action.Tick) { p.Pending=null; continue; }
                var spec=WeaponCatalog.Get(pending.Weapon);
                if(pending.Weapon==WeaponKind.PushLens)
                {
                    foreach(var target in players)
                    {
                        var delta=target.Eye-p.Eye;
                        if(!target.Alive || target.Team==p.Team || delta.sqrMagnitude>spec.Range*spec.Range ||
                            Vector3.Dot(delta.normalized,pending.Direction)<.8870108f || !world.Visible(p.Eye,target.Eye)) continue;
                        var flat=new Vector3(pending.Direction.x,0,pending.Direction.z).normalized;
                        Emit(CombatEventKind.Impulse,pending.Action,target.Participant,position:target.Feet,vector:flat*14+Vector3.up*4);
                    }
                    p.Pending=null;
                }
                else
                {
                    // Sweep both reach and the changing tip; world cover and per-swing hit mask govern each sample.
                    Vector3 tip=p.Eye+p.Aim*spec.Range;
                    if(pending.LastSampleTick==Tick-1) MeleeSweep(p,pending,world,pending.PreviousTip,tip-pending.PreviousTip,spec);
                    MeleeSweep(p,pending,world,p.Eye,p.Aim*spec.Range,spec);
                    pending.PreviousTip=tip; pending.LastSampleTick=Tick;
                    if(Tick>=pending.EndTick) p.Pending=null;
                }
            }
        }
        void MeleeSweep(CombatState owner,PendingAttack attack,ICombatWorld world,Vector3 origin,Vector3 delta,WeaponSpec spec)
        {
            int ignored=attack.HitMask;
            for(int i=0;i<8;i++)
            {
                if(!world.Sweep(origin,delta,.25f,owner.Participant,ignored,out var hit) || hit.Participant==0) break;
                ignored|=1<<hit.Participant; var target=Find(hit.Participant);
                if(target==null || !target.Alive || target.Team==owner.Team || (target.Eye-owner.Eye).sqrMagnitude>(spec.Range+.65f)*(spec.Range+.65f) ||
                    Vector3.Dot((target.Eye-owner.Eye).normalized,owner.Aim)<.5f || !world.Visible(owner.Eye,hit.Point)) continue;
                attack.HitMask|=1<<target.Participant; Hit(attack.Action,owner.Team,target,spec.Damage,world,false,hit.Point);
            }
        }
        void Hit(CombatAction action,int team,CombatState target,float damage,ICombatWorld world,bool stun,Vector3 point,bool selfDamage=false)
        {
            if(target==null || !target.Alive || (target.Team==team && !(selfDamage && target.Participant==action.Participant))) return;
            float prior=target.Shield, dealt=CombatRules.Damage(target,damage);
            Emit(CombatEventKind.Hit,action,target.Participant,dealt,point);
            if(prior>0 && target.Shield==0) Emit(CombatEventKind.ShieldBroken,action,target.Participant);
            if(stun && CombatRules.Stun(target,Tick,TickRate)) Emit(CombatEventKind.Stunned,action,target.Participant);
        }
        void AdvanceProjectiles(ICombatWorld world)
        {
            float dt=1f/TickRate;
            for(int i=projectiles.Count-1;i>=0;i--)
            {
                var shot=projectiles[i]; var spec=WeaponCatalog.Get(shot.Weapon); bool remove=false;
                if(Tick>=shot.ExpireTick)
                {
                    if(shot.Weapon==WeaponKind.Grenade || shot.Weapon==WeaponKind.PulseLobber) Explode(shot,world);
                    ReleaseProjectile(i); continue;
                }
                if(shot.Weapon==WeaponKind.Grenade) shot.Velocity+=Vector3.down*(18*dt);
                Vector3 delta=shot.Velocity*dt; float remaining=delta.magnitude;
                if(shot.Weapon!=WeaponKind.Grenade) remaining=Math.Min(remaining,Math.Max(0,spec.Range-shot.Travel));
                for(int bounce=0;bounce<4 && remaining>.0001f;bounce++)
                {
                    var direction=shot.Velocity.normalized;
                    if(!world.Sweep(shot.Position,direction*remaining,.12f,shot.Action.Participant,shot.HitMask,out var hit))
                    { shot.Position+=direction*remaining; shot.Travel+=remaining; break; }
                    float traveled=Math.Max(0,Math.Min(remaining,hit.Distance)); shot.Position+=direction*traveled; shot.Travel+=traveled; remaining-=traveled;
                    if(shot.Weapon==WeaponKind.PulseLobber) { Explode(shot,world); remove=true; break; }
                    if(hit.Participant!=0 && shot.Weapon!=WeaponKind.Grenade)
                    {
                        shot.HitMask|=1<<hit.Participant; Hit(shot.Action,shot.OwnerTeam,Find(hit.Participant),spec.Damage,world,shot.Weapon==WeaponKind.StunGun,hit.Point);
                        if(shot.Weapon==WeaponKind.StunGun) { remove=true; break; }
                        shot.Position+=direction*.005f; remaining=Math.Max(0,remaining-.005f); continue;
                    }
                    if(shot.Weapon==WeaponKind.Grenade)
                    {
                        shot.Velocity=Vector3.Reflect(shot.Velocity,hit.Normal)*.55f;
                        shot.Position+=hit.Normal*.005f; break;
                    }
                    if(shot.Weapon==WeaponKind.ReboundDisc && shot.Bounces<2)
                    { shot.Bounces++; shot.Velocity=Vector3.Reflect(shot.Velocity,hit.Normal); shot.Position+=hit.Normal*.005f; Emit(CombatEventKind.Ricochet,shot.Action,position:shot.Position,subIndex:shot.Bounces); }
                    else { remove=true; break; }
                }
                if(!remove && shot.Travel>=spec.Range && shot.Weapon!=WeaponKind.Grenade)
                { if(shot.Weapon==WeaponKind.PulseLobber) Explode(shot,world); remove=true; }
                if(remove) ReleaseProjectile(i);
            }
        }
        void ReleaseProjectile(int index)
        { freeProjectiles[freeProjectileCount++]=projectiles[index]; projectiles.RemoveAt(index); }
        void ResetProjectilePool()
        { projectiles.Clear(); for(int i=0;i<projectileStorage.Length;i++) freeProjectiles[i]=projectileStorage[i]; freeProjectileCount=freeProjectiles.Length; }
        void Explode(ArenaProjectile shot,ICombatWorld world)
        {
            var spec=WeaponCatalog.Get(shot.Weapon); Emit(CombatEventKind.Explosion,shot.Action,position:shot.Position,amount:spec.Radius);
            foreach(var p in players)
            {
                float distance=(p.Eye-shot.Position).magnitude;
                if(!p.Alive || distance>spec.Radius || !world.Visible(shot.Position,p.Eye) || (p.Team==shot.OwnerTeam && p.Participant!=shot.Action.Participant)) continue;
                float damage=spec.Damage+(spec.EdgeDamage-spec.Damage)*(distance/spec.Radius);
                Hit(shot.Action,shot.OwnerTeam,p,damage,world,false,p.Eye,true);
                Vector3 impulse=(p.Eye-shot.Position).normalized*(8*(1-distance/spec.Radius))+Vector3.up*2;
                Emit(CombatEventKind.Impulse,shot.Action,p.Participant,position:p.Feet,vector:impulse);
            }
        }
        void Kill(CombatState p)
        {
            p.Health=0; p.Alive=false; p.RespawnTick=Tick+Ticks(WeaponCatalog.RespawnSeconds); CombatRules.ClearLife(p);
            Emit(CombatEventKind.Death,SystemAction(p),p.Participant,position:p.Feet);
        }
        void Respawn(CombatState p)
        {
            if(p.LifeGeneration>=int.MaxValue-1) { Phase=ArenaMatchPhase.Aborted; return; }
            CombatRules.ClearLife(p); p.Health=100; p.Alive=true; p.LifeGeneration++; p.LastActionSequence=-1;
            p.CapsuleHeight=1.8f;
            p.StarterAmmo=12; p.FireReadyTick=Tick; p.GrenadeReadyTick=Tick; p.RespawnTick=0;
            int count=0; for(int i=0;i<map.Spawns.Length;i++) if(map.Spawns[i].Team==p.Team) count++;
            if(count>0)
            {
                int index=(p.Participant-1)%count;
                for(int i=0;i<map.Spawns.Length;i++) if(map.Spawns[i].Team==p.Team && index--==0) { p.Feet=map.Spawns[i].Position; break; }
            }
            p.DiscontinuityTick=Tick; Emit(CombatEventKind.Respawn,SystemAction(p),p.Participant,position:p.Feet);
        }
        void ScoreZone()
        {
            Captor=-1; Contested=false;
            foreach(var p in players)
                if(p.Alive && p.Connected && Zone.Contains(ElapsedTicks,p.Feet,p.Supported,p.SupportPoint,p.SupportNormal))
                { if(Captor<0) Captor=p.Team; else if(Captor!=p.Team) Contested=true; }
            if(Contested) Captor=-1;
            if(!Practice && TeamCount()<2)
            {
                oppositionStableSince=-1;
                if(absenceSince<0) absenceSince=Tick;
                if(Tick-absenceSince>=30*TickRate) Phase=ArenaMatchPhase.Aborted;
                return;
            }
            if(absenceSince>=0)
            {
                if(oppositionStableSince<0) oppositionStableSince=Tick;
                if(Tick-oppositionStableSince>=5*TickRate) { absenceSince=-1; oppositionStableSince=-1; }
            }
            if(Captor>=0) scoreTicks[Captor]++;
            if((Captor>=0 && scoreTicks[Captor]>=120*TickRate) || ElapsedTicks>=480*TickRate)
            {
                int high=-1, ties=0;
                for(int i=0;i<scoreTicks.Length;i++)
                    if(scoreTicks[i]>high) { high=scoreTicks[i]; Winner=i; ties=1; } else if(scoreTicks[i]==high) ties++;
                if(ties>1) Winner=-1;
                Phase=ArenaMatchPhase.Finished;
            }
        }
        public MatchSnapshot Capture() => new MatchSnapshot { MapId=map.Id,MapRevision=map.Revision,CatalogRevision=WeaponCatalog.Revision,TickRate=TickRate,
            Tick=Tick,StartTick=startTick,AuthorityGeneration=AuthorityGeneration,AbsenceSince=absenceSince,OppositionStableSince=oppositionStableSince,Winner=Winner,Practice=Practice,Phase=Phase,
            ScoreTicks=(int[])scoreTicks.Clone(),Players=players.Select(p=>p.Copy()).ToArray(),Pickups=pickups.Select(p=>p.Copy()).ToArray(),Projectiles=projectiles.Select(p=>p.Copy()).ToArray() };
        public bool TryRestore(MatchSnapshot saved)
        {
            if(saved==null || saved.MapId!=map.Id || saved.MapRevision!=map.Revision || saved.CatalogRevision!=WeaponCatalog.Revision || saved.TickRate!=TickRate || saved.Practice!=Practice ||
                saved.Tick<0 || saved.Tick>int.MaxValue-240000 || saved.StartTick<0 || saved.StartTick>saved.Tick || saved.AuthorityGeneration<1 ||
                saved.AbsenceSince< -1 || saved.AbsenceSince>saved.Tick || saved.Winner< -1 || saved.Winner>3 || !Enum.IsDefined(typeof(ArenaMatchPhase),saved.Phase) ||
                saved.OppositionStableSince< -1 || saved.OppositionStableSince>saved.Tick || (saved.OppositionStableSince>=0 && saved.AbsenceSince<0) ||
                saved.Players==null || saved.Players.Length>8 || saved.Pickups==null || saved.Pickups.Length!=pickups.Count ||
                saved.Projectiles==null || saved.Projectiles.Length>WeaponCatalog.MaximumProjectiles || saved.ScoreTicks==null || saved.ScoreTicks.Length!=4 ||
                saved.ScoreTicks.Any(s=>s<0 || s>120*TickRate)) return false;
            var ids=new HashSet<int>(); var teams=new int[4];
            foreach(var p in saved.Players)
            {
                if(!CombatRules.ValidPlayer(p) || !ids.Add(p.Participant) || ++teams[p.Team]>2 || p.DiscontinuityTick>saved.Tick ||
                    p.ShieldUntil>saved.Tick+Ticks(WeaponCatalog.ShieldSeconds) || (p.Shield>0 && p.ShieldUntil<=saved.Tick) ||
                    p.StunUntil>saved.Tick+Ticks(.45f) || p.ImmunityUntil>saved.Tick+Ticks(2.2f) ||
                    p.FireReadyTick>saved.Tick+Ticks(1.2f) || p.GrenadeReadyTick>saved.Tick+Ticks(.7f) || p.ReloadUntil>saved.Tick+Ticks(WeaponCatalog.ReloadSeconds) ||
                    p.RespawnTick>saved.Tick+Ticks(WeaponCatalog.RespawnSeconds) || p.DisconnectUntil>saved.Tick+5*TickRate ||
                    (p.Pending!=null && (p.Pending.Action.Tick>saved.Tick || p.Pending.Action.AuthorityGeneration<1 || p.Pending.Action.AuthorityGeneration>saved.AuthorityGeneration ||
                        p.Pending.EndTick<saved.Tick || p.Pending.EndTick>saved.Tick+Ticks(.25f)))) return false;
            }
            for(int i=0;i<saved.Pickups.Length;i++)
            {
                var item=saved.Pickups[i]; var original=pickups[i];
                if(item==null || item.Id!=i || item.Generation<1 || item.Generation==int.MaxValue || item.AvailableTick<0 ||
                    item.AvailableTick>saved.Tick+Ticks(WeaponCatalog.PickupSeconds) || item.Item!=original.Item || item.Position!=original.Position) return false;
            }
            var actions=new HashSet<string>();
            foreach(var shot in saved.Projectiles)
            {
                if(shot==null || !shot.Valid(saved.Tick) || shot.Action.AuthorityGeneration>saved.AuthorityGeneration || !ids.Contains(shot.Action.Participant) ||
                    !actions.Add(shot.Action.Participant+":"+shot.Action.LifeGeneration+":"+shot.Action.Sequence)) return false;
                var owner=saved.Players.First(p=>p.Participant==shot.Action.Participant); var spec=WeaponCatalog.Get(shot.Weapon);
                if(shot.OwnerTeam!=owner.Team || shot.Action.LifeGeneration>owner.LifeGeneration || shot.Action.ConnectionGeneration>owner.ConnectionGeneration ||
                    (shot.Action.LifeGeneration==owner.LifeGeneration && shot.Action.Sequence>owner.LastActionSequence) ||
                    shot.ExpireTick!=shot.Action.Tick+Ticks(spec.Lifetime) || !CombatRules.Direction(shot.Action.Direction) ||
                    (shot.Weapon!=WeaponKind.Grenade && shot.Travel>spec.Range) ||
                    (shot.Weapon!=WeaponKind.ReboundDisc && shot.Bounces!=0)) return false;
            }
            // Validate everything before replacing anything; clone so callers cannot mutate an installed checkpoint.
            var restoredPlayers=saved.Players.Select(p=>p.Copy()).OrderBy(p=>p.Participant).ToArray();
            var restoredPickups=saved.Pickups.Select(p=>p.Copy()).ToArray(); var restoredShots=saved.Projectiles.Select(p=>p.Copy()).ToArray();
            players.Clear(); players.AddRange(restoredPlayers); pickups.Clear(); pickups.AddRange(restoredPickups); ResetProjectilePool();
            foreach(var savedShot in restoredShots) { var shot=freeProjectiles[--freeProjectileCount]; shot.CopyFrom(savedShot); projectiles.Add(shot); }
            scoreTicks=(int[])saved.ScoreTicks.Clone(); Tick=saved.Tick; startTick=saved.StartTick; AuthorityGeneration=saved.AuthorityGeneration;
            absenceSince=saved.AbsenceSince; oppositionStableSince=saved.OppositionStableSince; Winner=saved.Winner; Phase=saved.Phase; events.Clear(); reservedProjectiles=0; Captor=-1; Contested=false;
            return true;
        }
        public bool AdvanceAuthorityGeneration(int generation)
        {
            if(generation<=AuthorityGeneration) return false;
            AuthorityGeneration=generation;
            oppositionStableSince=-1;
            // Committed objects retain their original action IDs; only new requests use the new epoch.
            return true;
        }
    }
}
