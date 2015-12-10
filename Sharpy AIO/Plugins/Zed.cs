using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LeagueSharp;
using LeagueSharp.Common;
using ItemData = LeagueSharp.Common.Data.ItemData;
using Color = System.Drawing.Color;
using SharpDX;

namespace Sharpy_AIO.Plugins
{
    public class Zed
    {
        private Menu Menu;
        private Orbwalking.Orbwalker Orbwalker;
        private Obj_AI_Hero Player = ObjectManager.Player;
        private Spell Q, W, E, R;
        private Vector3 rshadowpos;
        private SpellSlot Ignite = ObjectManager.Player.GetSpellSlot("summonerDot");
        private int LastSwitch;
        private Obj_AI_Minion shadow
        {
            get
            {
                return ObjectManager.Get<Obj_AI_Minion>().FirstOrDefault(x => x.IsVisible && x.IsAlly && x.Name == "Shadow" && !x.IsDead);
            }
        }

        private enum wCheck
        {
            First,
            Second,
            Cooltime
        }

        private enum rCheck
        {
            First,
            Second,
            Cooltime
        }

        private wCheck wReady
        {
            get
            {
                if (!W.IsReadyPerfectly())
                {
                    return
                        wCheck.Cooltime;
                }
                return
                    (Player.Spellbook.GetSpell(SpellSlot.W).Name == "ZedW" ? wCheck.First : wCheck.Second);
            }
        }

        private rCheck rReady
        {
            get
            {
                if (!R.IsReadyPerfectly())
                {
                    return
                        rCheck.Cooltime;
                }
                return
                    (Player.Spellbook.GetSpell(SpellSlot.R).Name == "ZedR" ? rCheck.First : rCheck.Second);
            }
        }

        public Zed()
        {
            Game.PrintChat("Sharpy AIO :: Zed Loaded :)");

            Q = new Spell(SpellSlot.Q, 900f, TargetSelector.DamageType.Physical) { MinHitChance = HitChance.High };
            W = new Spell(SpellSlot.W, 700f);
            E = new Spell(SpellSlot.E, 290f,TargetSelector.DamageType.Physical);
            R = new Spell(SpellSlot.R, 625f, TargetSelector.DamageType.Physical);

            Q.SetSkillshot(.25f, 70f, 1750f, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(.25f, 0f, 1750f, false, SkillshotType.SkillshotCircle);
            R.SetTargetted(0f, float.MaxValue);

            // 메인 메뉴
            Menu = new Menu("Sharpy AIO :: Zed", "mainmenu", true);

            // 오브워커 메뉴
            Menu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Orbwalker = new Orbwalking.Orbwalker(Menu.SubMenu("Orbwalker"));

            // 콤보 메뉴
            var combo = new Menu("Combo", "Combo");
            combo.AddItem(new MenuItem("CQ", "Use Q").SetValue(true));
            combo.AddItem(new MenuItem("CW", "Use W (Line Combo Only)").SetValue(true));
            combo.AddItem(new MenuItem("CE", "Use E").SetValue(true));
            combo.AddItem(new MenuItem("CI", "Use Item").SetValue(true));
            combo.AddItem(new MenuItem("CM", "Combo Mode").SetValue(new StringList(new[] { "Normal", "Line" }, 0)));
            Menu.AddSubMenu(combo);

            // 궁극기 메뉴
            var ult = new Menu("Ult Setting", "Ult Setting");
            ult.AddItem(new MenuItem("UR", "Cast R").SetValue(new KeyBind('T', KeyBindType.Press)));
            ult.AddItem(new MenuItem("UO", "Cast R Only Selected").SetValue(true));
            combo.AddSubMenu(ult);

            // 견제 메뉴
            var harass = new Menu("Harass", "Harass");
            harass.AddItem(new MenuItem("HQ", "Use Q").SetValue(true));
            harass.AddItem(new MenuItem("HW1", "Use W").SetValue(new KeyBind('Y', KeyBindType.Toggle)));
            harass.AddItem(new MenuItem("HW2", "Use W2").SetValue(false));
            harass.AddItem(new MenuItem("HE", "Use E").SetValue(true));
            harass.AddItem(new MenuItem("HI", "Use Item").SetValue(true));
            harass.AddItem(new MenuItem("HA", "Auto Harass Long Poke").SetValue(new KeyBind('G', KeyBindType.Toggle)));
            Menu.AddSubMenu(harass);

            // 이동 메뉴
            var flee = new Menu("Flee", "Flee");
            flee.AddItem(new MenuItem("FW", "Use W").SetValue(true));
            flee.AddItem(new MenuItem("FI", "Use Item").SetValue(true));
            Menu.AddSubMenu(flee);

            // 막타 메뉴
            var lasthit = new Menu("LastHit", "LastHit");
            lasthit.AddItem(new MenuItem("LHQ", "Use Q (Long)").SetValue(true));
            lasthit.AddItem(new MenuItem("LHE", "Use E (Short)").SetValue(true));
            Menu.AddSubMenu(lasthit);

            // 라인클리어 메뉴
            var laneclear = new Menu("LaneClear", "LaneClear");
            laneclear.AddItem(new MenuItem("LCQ", "Use Q").SetValue(true));
            laneclear.AddItem(new MenuItem("LCE", "Use E").SetValue(true));
            laneclear.AddItem(new MenuItem("LCI", "Use Item").SetValue(true));
            Menu.AddSubMenu(laneclear);

            // 정글클리어 메뉴
            var jungleclear = new Menu("JungleClear", "JungleClear");
            jungleclear.AddItem(new MenuItem("JCQ", "Use Q").SetValue(true));
            jungleclear.AddItem(new MenuItem("JCE", "Use E").SetValue(true));
            jungleclear.AddItem(new MenuItem("JCI", "Use Item").SetValue(true));
            Menu.AddSubMenu(jungleclear);

            // 기타 메뉴
            var misc = new Menu("Misc", "Misc");
            misc.AddItem(new MenuItem("MK", "Use Killsteal").SetValue(true));
            misc.AddItem(new MenuItem("ME", "Auto Shadow E").SetValue(true));
            Menu.AddSubMenu(misc);

            // 킬스틸 메뉴
            var killsteal = new Menu("Killsteal Setting","Killsteal Setting");
            killsteal.AddItem(new MenuItem("K0", "If Only On Shadow"));
            killsteal.AddItem(new MenuItem("KQ", "Use Q").SetValue(true));
            killsteal.AddItem(new MenuItem("KE", "Use E").SetValue(true));
            killsteal.AddItem(new MenuItem("KI", "Use Ignite").SetValue(true));
            misc.AddSubMenu(killsteal);

            // 드로잉 메뉴
            var drawing = new Menu("Drawing", "Drawing");
            drawing.AddItem(new MenuItem("DQ", "Draw Q Range").SetValue(new Circle(true, Color.Green)));
            drawing.AddItem(new MenuItem("DW", "Draw W Range").SetValue(new Circle(true, Color.Green)));
            drawing.AddItem(new MenuItem("DE", "Draw E Range").SetValue(new Circle(true, Color.Green)));
            drawing.AddItem(new MenuItem("DR", "Draw R Range").SetValue(new Circle(true, Color.Green)));
            drawing.AddItem(new MenuItem("DWQ", "Draw WQ Range").SetValue(new Circle(true, Color.Green)));
            drawing.AddItem(new MenuItem("DS", "Draw Combo Mode").SetValue(true));
            Menu.AddSubMenu(drawing);

            // 그림자 드로잉 메뉴
            var sd = new Menu("Shadow Drawing", "Shadow Drawing");
            sd.AddItem(new MenuItem("WQ", "Shadow Q Range").SetValue(new Circle(true, Color.WhiteSmoke)));
            sd.AddItem(new MenuItem("WE", "Shadow E Range").SetValue(new Circle(true, Color.WhiteSmoke)));
            drawing.AddSubMenu(sd);

            Menu.AddToMainMenu();

            new DamageIndicator();
            DamageIndicator.DamageToUnit = getcombodamage;

            Game.OnUpdate += Game_OnUpdate;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            var DQ = Menu.Item("DQ").GetValue<Circle>();
            if (DQ.Active)
            {
                if (Q.IsReadyPerfectly())
                {
                    Render.Circle.DrawCircle(Player.Position, Q.Range, DQ.Color, 3);
                }
            }

            var DW = Menu.Item("DW").GetValue<Circle>();
            if (DW.Active)
            {
                if (W.IsReadyPerfectly())
                {
                    Render.Circle.DrawCircle(Player.Position, W.Range, DW.Color, 3);
                }
            }

            var DE = Menu.Item("DE").GetValue<Circle>();
            if (DE.Active)
            {
                if (E.IsReadyPerfectly())
                {
                    Render.Circle.DrawCircle(Player.Position, E.Range, DE.Color, 3);
                }
            }

            var DR = Menu.Item("DR").GetValue<Circle>();
            if (DR.Active)
            {
                if (R.IsReadyPerfectly())
                {
                    if (R.IsReadyPerfectly())
                    {
                        Render.Circle.DrawCircle(Player.Position, R.Range, DR.Color, 3);
                    }
                }
            }

            var DWQ = Menu.Item("DWQ").GetValue<Circle>();
            if (DWQ.Active)
            {
                if (Q.IsReadyPerfectly() && W.IsReadyPerfectly())
                {
                    Render.Circle.DrawCircle(Player.Position, Q.Range + W.Range, DWQ.Color, 3);
                }
            }

            var WQ = Menu.Item("WQ").GetValue<Circle>();
            if (WQ.Active)
            {
                if (shadow != null)
                {
                    if (Q.IsReadyPerfectly())
                    {
                        Render.Circle.DrawCircle(shadow.Position, Q.Range, WQ.Color, 3);
                    }
                }
            }

            var WE = Menu.Item("WE").GetValue<Circle>();
            if (WE.Active)
            {
                if (shadow != null)
                {
                    if (E.IsReadyPerfectly())
                    {
                        Render.Circle.DrawCircle(shadow.Position, E.Range, WE.Color, 3);
                    }
                }
            }

            if (Menu.Item("DS").GetValue<bool>())
            {
                var position = Drawing.WorldToScreen(ObjectManager.Player.Position);
                switch (Menu.Item("CM").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        Drawing.DrawText(position.X, position.Y + 40, Color.White,"Combo Mode : Normal");
                        break;

                    case 1:
                        Drawing.DrawText(position.X, position.Y + 40, Color.White, "Combo Mode : Line");
                        break; 
                }
            }
        }

        private void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (args.Unit.IsMe)
            {
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
                {
                    if (Player.Mana >= E.ManaCost)
                    {
                        if (Menu.Item("LHE").GetValue<bool>())
                        {
                            if (E.IsReadyPerfectly())
                            {
                                args.Process = false;
                            }
                        }
                    }
                }
            }
        }

        private void Game_OnUpdate(EventArgs args)
        {
            if (Orbwalking.CanMove(20))
            {
                if (Menu.Item("UR").GetValue<KeyBind>().Active)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);

                    if (R.IsReadyPerfectly() && rReady == rCheck.First)
                    {
                        if (Menu.Item("CI").GetValue<bool>())
                        {
                            var target = TargetSelector.GetTarget(1300f, TargetSelector.DamageType.Physical);
                            if (target != null)
                            {
                                castYoumuu();
                            }
                        }

                        if (Menu.Item("UO").GetValue<bool>())
                        {
                            var target = TargetSelector.GetSelectedTarget();
                            if (Player.Position.Distance(target.Position) <= R.Range && !target.IsZombie)
                            {
                                R.CastOnUnit(target);
                            }
                        }
                        else
                        {
                            var target = TargetSelector.GetTarget(R.Range, R.DamageType);
                            if (target != null)
                            {
                                R.CastOnUnit(target);
                            }
                        }
                    }
                }

                switch (Orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Flee:
                        {
                            if (Menu.Item("FI").GetValue<bool>())
                            {
                                castYoumuu();
                            }

                            if (Menu.Item("FW").GetValue<bool>())
                            {
                                if (Player.Mana >= W.ManaCost)
                                {
                                    if (W.IsReadyPerfectly())
                                    {
                                        if (wReady == wCheck.First)
                                        {
                                            W.Cast(Game.CursorPos);
                                        }
                                    }
                                }

                                if (wReady == wCheck.Second)
                                {
                                    W.Cast();
                                }
                            }
                        }
                        break;
                    case Orbwalking.OrbwalkingMode.LastHit:
                        {
                            if (Menu.Item("LHQ").GetValue<bool>())
                            {
                                if (Player.Mana >= Q.ManaCost)
                                {
                                    if (Q.IsReadyPerfectly())
                                    {
                                        var target = MinionManager.GetMinions(Q.Range).FirstOrDefault(x => x.IsKillableAndValidTarget(Q.GetDamage(x), Q.DamageType, Q.Range));
                                        if (target != null)
                                        {
                                            if (Player.Position.Distance(target.Position) > 125f)
                                            {
                                                Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                Q.Cast(target);
                                            }
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("LHE").GetValue<bool>())
                            {
                                if (Player.Mana >= E.ManaCost)
                                {
                                    if (E.IsReadyPerfectly())
                                    {
                                        var target = MinionManager.GetMinions(E.Range).FirstOrDefault(x => x.IsKillableAndValidTarget(E.GetDamage(x), E.DamageType, E.Range));
                                        if (target != null)
                                        {
                                            E.Cast();
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        {
                            var starget = TargetSelector.GetSelectedTarget();
                            var WEQMana = W.ManaCost + E.ManaCost + Q.ManaCost;
                            if (Menu.Item("HW1").GetValue<KeyBind>().Active)
                            {
                                if (W.IsReadyPerfectly() && wReady == wCheck.First)
                                {
                                    if (Player.Mana >= WEQMana)
                                    {
                                        if (starget != null && Player.Position.Distance(starget.Position) <= W.Range)
                                        {
                                            if (!starget.IsZombie)
                                            {
                                                W.Cast(starget);
                                            }
                                        }
                                        else
                                        {
                                            var target = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
                                            if (target != null)
                                            {
                                                W.Cast(target);
                                            }
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("HE").GetValue<bool>())
                            {
                                if (E.IsReadyPerfectly())
                                {
                                    if (shadow != null)
                                    {
                                        if (starget != null && Player.Position.Distance(starget.Position) <= E.Range || starget != null && shadow.Position.Distance(starget.Position) <= E.Range)
                                        {
                                            if (!starget.IsZombie)
                                            {
                                                E.Cast();
                                            }
                                        }
                                        else
                                        {
                                            var target = TargetSelector.GetTarget(E.Range + Player.Position.Distance(shadow.Position), E.DamageType);
                                            if (target != null)
                                            {
                                                if (Player.Position.Distance(target.Position) <= E.Range || shadow.Position.Distance(target.Position) <= E.Range)
                                                {
                                                    E.Cast();
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!W.IsReadyPerfectly() || Player.Mana < WEQMana || !Menu.Item("HW1").GetValue<KeyBind>().Active)
                                        {
                                            if (starget != null && Player.Position.Distance(starget.Position) <= E.Range)
                                            {
                                                if (!starget.IsZombie)
                                                {
                                                    E.Cast();
                                                }
                                            }
                                            else
                                            {
                                                var target = TargetSelector.GetTarget(E.Range, E.DamageType);
                                                if (target != null)
                                                {
                                                    E.Cast();
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("HQ").GetValue<bool>())
                            {
                                if (Q.IsReadyPerfectly())
                                {
                                    if (shadow != null)
                                    {
                                        if (starget != null && Player.Position.Distance(starget.Position) <= Q.Range || starget != null && shadow.Position.Distance(starget.Position) <= Q.Range)
                                        {
                                            if (!starget.IsZombie)
                                            {
                                                if (shadow.Position.Distance(starget.Position) <= Q.Range)
                                                {
                                                    Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                                    Q.Cast(starget);
                                                }
                                                else
                                                {
                                                    if (Player.Position.Distance(starget.Position) <= Q.Range)
                                                    {
                                                        Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                        Q.Cast(starget);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var target = TargetSelector.GetTarget(Q.Range + Player.Position.Distance(shadow.Position), Q.DamageType);
                                            if (target != null)
                                            {
                                                if (shadow.Position.Distance(target.Position) <= Q.Range)
                                                {
                                                    Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                                    Q.Cast(target);
                                                }
                                                else
                                                {
                                                    if (Player.Position.Distance(target.Position) <= Q.Range)
                                                    {
                                                        Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                        Q.Cast(target);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!W.IsReadyPerfectly() || Player.Mana < WEQMana || !Menu.Item("HW1").GetValue<KeyBind>().Active)
                                        {
                                            if (starget != null && Player.Position.Distance(starget.Position) <= Q.Range)
                                            {
                                                if (!starget.IsZombie)
                                                {
                                                    Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                    Q.Cast(starget);
                                                }
                                            }
                                            else
                                            {
                                                var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                                                if (target != null)
                                                {
                                                    Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                    Q.Cast(target);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("HW2").GetValue<bool>())
                            {
                                if (Player.HasBuff("zedwhandler"))
                                {
                                    if (starget != null && shadow.Position.Distance(starget.Position) <= 125f)
                                    {
                                        if (!starget.IsZombie)
                                        {
                                            W.Cast();
                                            Player.IssueOrder(GameObjectOrder.AttackTo, starget);
                                        }
                                    }
                                    else
                                    {
                                        var target = TargetSelector.GetTarget(Player.Position.Distance(shadow.Position) + 125f, TargetSelector.DamageType.Physical);
                                        if (target != null)
                                        {
                                            W.Cast();
                                            Player.IssueOrder(GameObjectOrder.AttackTo, target);
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("HI").GetValue<bool>())
                            {
                                var btarget = TargetSelector.GetTarget(550f, TargetSelector.DamageType.Physical);
                                if (btarget != null)
                                {
                                    castBOTRK(btarget);
                                }

                                var target = TargetSelector.GetTarget(350f, TargetSelector.DamageType.Physical);
                                if (target != null)
                                {
                                    castHydra();
                                }
                            }
                        }
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        {
                            if (Player.Mana >= Q.ManaCost)
                            {
                                if (Q.IsReadyPerfectly())
                                {
                                    if (Menu.Item("LCQ").GetValue<bool>())
                                    {
                                        var target = Q.GetLineFarmLocation(MinionManager.GetMinions(Q.Range));
                                        if (target.MinionsHit >= 1)
                                        {
                                            Q.UpdateSourcePosition(Player.Position);
                                            Q.Cast(target.Position);
                                        }
                                    }

                                    if (Menu.Item("JCQ").GetValue<bool>())
                                    {
                                        var target = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth)
                                            .FirstOrDefault(x => x.IsValidTarget(Q.Range));
                                        if (target != null)
                                        {
                                            Q.UpdateSourcePosition(Player.Position);
                                            Q.Cast(target);
                                        }
                                    }
                                }
                            }

                            if (Player.Mana >= E.ManaCost)
                            {
                                if (E.IsReadyPerfectly())
                                {
                                    if (Menu.Item("LCE").GetValue<bool>())
                                    {
                                        var target = E.GetCircularFarmLocation(MinionManager.GetMinions(E.Range));
                                        if (target.MinionsHit >= 1)
                                        {
                                            E.Cast();
                                        }
                                    }

                                    if (Menu.Item("JCE").GetValue<bool>())
                                    {
                                        var target = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth)
                                            .FirstOrDefault(x => x.IsValidTarget(E.Range));
                                        if (target != null)
                                        {
                                            E.Cast();
                                        }
                                    }
                                }
                            }

                            if (Menu.Item("LCI").GetValue<bool>())
                            {
                                var target = MinionManager.GetMinions(400f, MinionTypes.All, MinionTeam.Enemy).FirstOrDefault(x => x.IsValidTarget(400f));
                                if (target != null)
                                {
                                    castHydra();
                                }
                            }

                            if (Menu.Item("JCI").GetValue<bool>())
                            {
                                var target = MinionManager.GetMinions(400f, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth)
                                    .FirstOrDefault(x => x.IsValidTarget(400f));
                                if (target != null)
                                {
                                    castHydra();
                                }
                            }
                        }
                        break;
                    case Orbwalking.OrbwalkingMode.Combo:
                        {
                            var starget = TargetSelector.GetSelectedTarget();
                            if (Menu.Item("CI").GetValue<bool>())
                            {
                                if (starget != null && Player.Position.Distance(starget.Position) <= 1500f)
                                {
                                    if (!starget.IsZombie)
                                    {
                                        castYoumuu();
                                    }
                                }
                                else
                                {
                                    var target = TargetSelector.GetTarget(1500f, TargetSelector.DamageType.Physical);
                                    if (target != null)
                                    {
                                        castYoumuu();
                                    }
                                }

                                if (starget != null && Player.Position.Distance(starget.Position) <= 550f)
                                {
                                    if (!starget.IsZombie)
                                    {
                                        castBOTRK(starget);
                                    }
                                }
                                else
                                {
                                    var target = TargetSelector.GetTarget(550f, TargetSelector.DamageType.Physical);
                                    if (target != null)
                                    {
                                        castBOTRK(target);
                                    }
                                }
                            }

                            switch (Menu.Item("CM").GetValue<StringList>().SelectedIndex)
                            {
                                case 0:
                                    if (shadow != null)
                                    {
                                        if (Menu.Item("CE").GetValue<bool>())
                                        {
                                            if (E.IsReadyPerfectly())
                                            {
                                                if (starget != null && Player.Position.Distance(starget.Position) <= E.Range || starget != null && shadow.Position.Distance(starget.Position) <= E.Range)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        E.Cast();
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(E.Range + Player.Position.Distance(shadow.Position), E.DamageType);
                                                    if (target != null)
                                                    {
                                                        if (Player.Position.Distance(target.Position) <= E.Range || shadow.Position.Distance(target.Position) <= E.Range)
                                                        {
                                                            E.Cast();
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        if (Menu.Item("CQ").GetValue<bool>())
                                        {
                                            if (Q.IsReadyPerfectly())
                                            {
                                                if (starget != null && Player.Position.Distance(starget.Position) <= Q.Range || starget != null && shadow.Position.Distance(starget.Position) <= Q.Range)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        if (shadow.Position.Distance(starget.Position) <= Q.Range)
                                                        {
                                                            Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                                            Q.Cast(starget);
                                                        }
                                                        else
                                                        {
                                                            if (Player.Position.Distance(starget.Position) <= Q.Range)
                                                            {
                                                                Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                                Q.Cast(starget);
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(Q.Range + Player.Position.Distance(shadow.Position), Q.DamageType);
                                                    if (target != null)
                                                    {
                                                        if (shadow.Position.Distance(target.Position) <= Q.Range)
                                                        {
                                                            Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                                            Q.Cast(target);
                                                        }
                                                        else
                                                        {
                                                            if (Player.Position.Distance(target.Position) <= Q.Range)
                                                            {
                                                                Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                                Q.Cast(target);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (Menu.Item("CQ").GetValue<bool>())
                                        {
                                            if (Q.IsReadyPerfectly())
                                            {
                                                if (starget != null && Player.Position.Distance(starget.Position) <= Q.Range)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                        Q.Cast(starget);
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                                                    if (target != null)
                                                    {
                                                        Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                        Q.Cast(target);
                                                    }
                                                }
                                            }
                                        }

                                        if (Menu.Item("CE").GetValue<bool>())
                                        {
                                            if (E.IsReadyPerfectly())
                                            {
                                                if (starget != null && Player.Position.Distance(starget.Position) <= E.Range)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        E.Cast();
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(E.Range, E.DamageType);
                                                    if (target != null)
                                                    {
                                                        E.Cast();
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (Menu.Item("CI").GetValue<bool>())
                                    {
                                        if (starget != null && Player.Position.Distance(starget.Position) <= 350f)
                                        {
                                            if (!starget.IsZombie)
                                            {
                                                castHydra();
                                            }
                                        }
                                        else
                                        {
                                            var target = TargetSelector.GetTarget(350f, TargetSelector.DamageType.Physical);
                                            if (target != null)
                                            {
                                                castHydra();
                                            }
                                        }
                                    }
                                    break;

                                case 1:
                                    if (rReady == rCheck.Second)
                                    {
                                        if (wReady == wCheck.First)
                                        {
                                            if (E.IsReadyPerfectly())
                                            {
                                                if (starget != null && Player.Position.Distance(starget.Position) <= E.Range)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        E.Cast();
                                                        Player.IssueOrder(GameObjectOrder.AttackTo, starget);
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(E.Range, E.DamageType);
                                                    if (target != null)
                                                    {
                                                        E.Cast();
                                                        Player.IssueOrder(GameObjectOrder.AttackTo, target);
                                                    }
                                                }
                                            }

                                            if (W.IsReadyPerfectly())
                                            {
                                                if (starget != null)
                                                {
                                                    if (!starget.IsZombie)
                                                    {
                                                        var spos = starget.Position.Extend(Player.Position, -650f);
                                                        W.Cast(spos);
                                                    }
                                                }
                                                else
                                                {
                                                    var target = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
                                                    if (target != null)
                                                    {
                                                        var wpos = target.Position.Extend(Player.Position, -650f);
                                                        W.Cast(wpos);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (wReady == wCheck.Second)
                                            {
                                                if (Q.IsReadyPerfectly())
                                                {
                                                    if (starget != null && Player.Position.Distance(starget.Position) <= Q.Range)
                                                    {
                                                        if (!starget.IsZombie)
                                                        {
                                                            Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                            Q.Cast(starget);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                                                        if (target != null)
                                                        {
                                                            Q.UpdateSourcePosition(Player.Position, Player.Position);
                                                            Q.Cast(target);
                                                        }
                                                    }
                                                }

                                                if (Menu.Item("CI").GetValue<bool>())
                                                {
                                                    if (starget != null && Player.Position.Distance(starget.Position) <= 350f)
                                                    {
                                                        if (!starget.IsZombie)
                                                        {
                                                            castHydra();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var target = TargetSelector.GetTarget(350f, TargetSelector.DamageType.Physical);
                                                        if (target != null)
                                                        {
                                                            castHydra();
                                                        }
                                                    }
                                                }

                                                if (!Q.IsReadyPerfectly())
                                                {
                                                    if (Utils.GameTimeTickCount - LastSwitch >= 350)
                                                    {
                                                        Menu.Item("CM").SetValue(new StringList(new[] { "Normal", "Line" }, 0));
                                                        LastSwitch = Utils.GameTimeTickCount;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                    case Orbwalking.OrbwalkingMode.None:
                        break;
                }

                if (Menu.Item("HA").GetValue<KeyBind>().Active)
                {
                    if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo)
                    {
                        var starget = TargetSelector.GetSelectedTarget();
                        if (W.IsReadyPerfectly() && wReady == wCheck.First)
                        {
                            if (Player.Mana >= Q.ManaCost + W.ManaCost)
                            {
                                if (Q.IsReadyPerfectly())
                                {
                                    if (starget != null && Player.Position.Distance(starget.Position) <= W.Range + Q.Range)
                                    {
                                        if (!starget.IsZombie)
                                        {
                                            if (Player.Position.Distance(starget.Position) > W.Range)
                                            {
                                                var spos = starget.Position.Extend(Player.Position, -(starget.Position.Distance(Player.Position) + W.Range));
                                                W.Cast(spos);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var target = TargetSelector.GetTarget(Q.Range + W.Range, TargetSelector.DamageType.Physical);
                                        if (target != null)
                                        {
                                            if (Player.Position.Distance(target.Position) > W.Range)
                                            {
                                                var wpos = target.Position.Extend(Player.Position, -(target.Position.Distance(Player.Position) + W.Range));
                                                W.Cast(wpos);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (Q.IsReadyPerfectly())
                        {
                            if (shadow != null)
                            {
                                if (starget != null && shadow.Position.Distance(starget.Position) <= Q.Range)
                                {
                                    if (!starget.IsZombie)
                                    {
                                        Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                        Q.Cast(starget);
                                    }
                                }
                                else
                                {
                                    var target = TargetSelector.GetTarget(Q.Range + Player.Position.Distance(shadow.Position), Q.DamageType);
                                    if (target != null)
                                    {
                                        if (shadow.Position.Distance(target.Position) <= Q.Range)
                                        {
                                            Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                            Q.Cast(target);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (Menu.Item("ME").GetValue<bool>())
                {
                    if (E.IsReadyPerfectly())
                    {
                        if (shadow != null)
                        {
                            var starget = TargetSelector.GetSelectedTarget();
                            if (starget != null && shadow.Position.Distance(starget.Position) <= E.Range)
                            {
                                if (!starget.IsZombie)
                                {
                                    E.Cast();
                                }
                            }
                            else
                            {
                                var target = TargetSelector.GetTarget(E.Range + Player.Position.Distance(shadow.Position), E.DamageType);
                                if (target != null)
                                {
                                    if (shadow.Position.Distance(target.Position) <= E.Range)
                                    {
                                        E.Cast();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Killsteal();
        }

        private void Killsteal()
        {
            if (Menu.Item("MK").GetValue<bool>())
            {
                if (shadow != null)
                {
                    if (Menu.Item("KQ").GetValue<bool>())
                    {
                        if (Q.IsReadyPerfectly())
                        {
                            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsKillableAndValidTarget(Q.GetDamage(x), Q.DamageType, Q.Range + Player.Position.Distance(shadow.Position)));
                            if (target != null)
                            {
                                if (shadow.Position.Distance(target.Position) <= Q.Range)
                                {
                                    Q.UpdateSourcePosition(shadow.Position, shadow.Position);
                                    Q.Cast(target);
                                }
                            }
                        }
                    }

                    if (Menu.Item("KE").GetValue<bool>())
                    {
                        if (E.IsReadyPerfectly())
                        {
                            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsKillableAndValidTarget(E.GetDamage(x), E.DamageType, Player.Position.Distance(shadow.Position) + E.Range));
                            if (target != null)
                            {
                                if (shadow.Position.Distance(target.Position) <= E.Range)
                                {
                                    E.Cast();
                                }
                            }
                        }
                    }
                }

                if (Menu.Item("KI").GetValue<bool>())
                {
                    var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsKillableAndValidTarget(Player.GetSummonerSpellDamage(x, Damage.SummonerSpell.Ignite), TargetSelector.DamageType.True, 600f));
                    if (target != null)
                    {
                        if (!target.IsZombie)
                        {
                            if (Ignite != SpellSlot.Unknown)
                            {
                                if (Ignite.IsReady())
                                {
                                    Player.Spellbook.CastSpell(Ignite, target.Position);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void castBOTRK(Obj_AI_Hero target)
        {
            var bilge = ItemData.Bilgewater_Cutlass.GetItem();
            var botrk = ItemData.Blade_of_the_Ruined_King.GetItem();

            if (bilge.IsReady() || botrk.IsReady())
            {
                bilge.Cast(target);
                botrk.Cast(target);
            }
        }

        private void castHydra()
        {
            var tiamet = ItemData.Tiamat_Melee_Only.GetItem();
            var hydra = ItemData.Ravenous_Hydra_Melee_Only.GetItem();

            if (tiamet.IsReady() || hydra.IsReady())
            {
                tiamet.Cast();
                hydra.Cast();
            }
        }

        private void castYoumuu()
        {
            var yomu = ItemData.Youmuus_Ghostblade.GetItem();
            if (yomu.IsReady())
            {
                yomu.Cast();
            }
        }

        private float getcombodamage(Obj_AI_Base enemy)
        {
            float damage = 0;

            if (ItemData.Bilgewater_Cutlass.GetItem().IsReady())
            {
                damage += (float)Player.GetItemDamage(enemy, Damage.DamageItems.Bilgewater);
            }

            if (ItemData.Blade_of_the_Ruined_King.GetItem().IsReady())
            {
                damage += (float)Player.GetItemDamage(enemy, Damage.DamageItems.Botrk);
            }

            if (ItemData.Tiamat_Melee_Only.GetItem().IsReady())
            {
                damage += (float)Player.GetItemDamage(enemy, Damage.DamageItems.Tiamat);
            }

            if (ItemData.Ravenous_Hydra_Melee_Only.GetItem().IsReady())
            {
                damage += (float)Player.GetItemDamage(enemy, Damage.DamageItems.Hydra);
            }

            if (Q.IsReadyPerfectly())
            {
                damage += Q.GetDamage(enemy);
            }

            if (W.IsReadyPerfectly())
            {
                damage += Q.GetDamage(enemy) / 2;
            }

            if (E.IsReadyPerfectly())
            {
                damage += E.GetDamage(enemy);
            }

            if (R.IsReadyPerfectly())
            {
                damage += R.GetDamage(enemy);
                damage += (float)(R.Level * .15 + .05);
            }
            return damage;
        }
    }
}
