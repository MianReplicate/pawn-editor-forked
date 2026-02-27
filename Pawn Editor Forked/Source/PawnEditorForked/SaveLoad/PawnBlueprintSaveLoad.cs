using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnEditor;

/// <summary>
/// Blueprint format for saving/loading pawns. Stores defNames with MayRequire attributes
/// for cross-modlist portability. Missing mods/DLCs are gracefully skipped.
///
/// Save: Pawn → PawnBlueprint XML (defNames + MayRequire packageIds)
/// Load: PawnBlueprint XML → PawnGenerator fresh pawn → apply matching attributes
/// </summary>
public static class PawnBlueprintSaveLoad
{
    // ═══════════════════════════════════════════════════════════════
    //  SAVE — Pawn → Blueprint XML
    // ═══════════════════════════════════════════════════════════════

    public static void SaveBlueprint(Pawn pawn, string filePath)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = System.Text.Encoding.UTF8
        };

        using var writer = XmlWriter.Create(filePath, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("PawnBlueprint");
        writer.WriteAttributeString("version", "1");

        // Meta block for vanilla file picker version display
        writer.WriteStartElement("meta");
        WriteSimple(writer, "gameVersion", VersionControl.CurrentVersionStringWithRev);
        writer.WriteEndElement();

        WriteIdentity(writer, pawn);
        WriteStory(writer, pawn);
        WriteAppearance(writer, pawn);
        WriteStyle(writer, pawn);
        WriteTraits(writer, pawn);
        WriteGenes(writer, pawn);
        WriteSkills(writer, pawn);
        WriteHediffs(writer, pawn);
        WriteAbilities(writer, pawn);
        WriteApparel(writer, pawn);

        writer.WriteEndElement(); // PawnBlueprint
        writer.WriteEndDocument();

        // Save portrait alongside
        try { PawnEditor.SavePawnTex(pawn, Path.ChangeExtension(filePath, ".png"), Rot4.South); }
        catch { }
    }

    // ── Save: Identity ──

    private static void WriteIdentity(XmlWriter w, Pawn pawn)
    {
        // Name
        if (pawn.Name is NameTriple nt)
        {
            w.WriteStartElement("name");
            w.WriteAttributeString("first", nt.First ?? "");
            w.WriteAttributeString("nick", nt.Nick ?? "");
            w.WriteAttributeString("last", nt.Last ?? "");
            w.WriteEndElement();
        }
        else if (pawn.Name != null)
        {
            WriteSimple(w, "name", pawn.Name.ToStringFull);
        }

        WriteSimple(w, "gender", pawn.gender.ToString());
        WriteSimple(w, "biologicalAge", pawn.ageTracker.AgeBiologicalYearsFloat.ToString("F2"));
        WriteSimple(w, "chronologicalAge", pawn.ageTracker.AgeChronologicalYearsFloat.ToString("F2"));

        // KindDef
        WriteDefWithSource(w, "kindDef", pawn.kindDef);

        // Faction type (for reference, not binding)
        if (pawn.Faction != null)
            WriteSimple(w, "factionDefName", pawn.Faction.def.defName);

        // Ideology
        if (ModsConfig.IdeologyActive && pawn.Ideo != null)
        {
            WriteSimple(w, "ideoName", pawn.Ideo.name);
            w.WriteStartElement("ideoCertainty");
            w.WriteString(pawn.ideo.Certainty.ToString("F4"));
            w.WriteEndElement();
        }

        // Xenotype metadata
        if (ModsConfig.BiotechActive && pawn.genes != null)
        {
            if (pawn.genes.Xenotype != null)
                WriteDefWithSource(w, "xenotypeDef", pawn.genes.Xenotype);
            if (!pawn.genes.xenotypeName.NullOrEmpty())
                WriteSimple(w, "xenotypeName", pawn.genes.xenotypeName);
            if (pawn.genes.iconDef != null)
                WriteDefWithSource(w, "xenotypeIconDef", pawn.genes.iconDef);

            // Biotech age data
            WriteSimple(w, "growthPoints", pawn.ageTracker.growthPoints.ToString("F2"));
        }

        // Favorite color
        if (pawn.story?.favoriteColor != null)
        {
            var favColor = pawn.story.favoriteColor.color;
            WriteColor(w, "favoriteColor", favColor);
        }
    }

    // ── Save: Story (backstories) ──

    private static void WriteStory(XmlWriter w, Pawn pawn)
    {
        if (pawn.story == null) return;

        if (pawn.story.Childhood != null)
            WriteDefWithSource(w, "childhood", pawn.story.Childhood);
        if (pawn.story.Adulthood != null)
            WriteDefWithSource(w, "adulthood", pawn.story.Adulthood);
    }

    // ── Save: Appearance ──

    private static void WriteAppearance(XmlWriter w, Pawn pawn)
    {
        if (pawn.story == null) return;

        w.WriteStartElement("appearance");

        if (pawn.story.bodyType != null) WriteDefWithSource(w, "bodyType", pawn.story.bodyType);
        if (pawn.story.headType != null) WriteDefWithSource(w, "headType", pawn.story.headType);
        if (pawn.story.hairDef != null) WriteDefWithSource(w, "hairDef", pawn.story.hairDef);
        if (pawn.story.furDef != null) WriteDefWithSource(w, "furDef", pawn.story.furDef);

        WriteColor(w, "hairColor", pawn.story.HairColor);
        WriteColor(w, "skinColorBase", pawn.story.SkinColorBase);
        if (pawn.story.skinColorOverride.HasValue)
            WriteColor(w, "skinColorOverride", pawn.story.skinColorOverride.Value);

        // Melanin
        WriteSimple(w, "melanin", pawn.story.melanin.ToString("F4"));

        w.WriteEndElement();
    }

    // ── Save: Style ──

    private static void WriteStyle(XmlWriter w, Pawn pawn)
    {
        if (pawn.style == null) return;

        w.WriteStartElement("style");
        if (pawn.style.beardDef != null) WriteDefWithSource(w, "beardDef", pawn.style.beardDef);
        if (ModsConfig.IdeologyActive)
        {
            if (pawn.style.BodyTattoo != null) WriteDefWithSource(w, "bodyTattoo", pawn.style.BodyTattoo);
            if (pawn.style.FaceTattoo != null) WriteDefWithSource(w, "faceTattoo", pawn.style.FaceTattoo);
        }
        w.WriteEndElement();
    }

    // ── Save: Traits ──

    private static void WriteTraits(XmlWriter w, Pawn pawn)
    {
        if (pawn.story?.traits?.allTraits == null) return;

        w.WriteStartElement("traits");
        foreach (var trait in pawn.story.traits.allTraits)
        {
            if (trait?.def == null) continue;
            // Skip gene-granted traits — genes will re-add them on load
            if (ModsConfig.BiotechActive && trait.sourceGene != null) continue;

            w.WriteStartElement("li");
            w.WriteAttributeString("defName", trait.def.defName);
            w.WriteAttributeString("degree", trait.Degree.ToString());
            if (trait.ScenForced) w.WriteAttributeString("scenForced", "true");
            WriteSourceMod(w, trait.def);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ── Save: Genes ──

    private static void WriteGenes(XmlWriter w, Pawn pawn)
    {
        if (!ModsConfig.BiotechActive || pawn.genes == null) return;

        w.WriteStartElement("genes");

        w.WriteStartElement("endogenes");
        foreach (var gene in pawn.genes.Endogenes)
        {
            if (gene?.def == null) continue;
            w.WriteStartElement("li");
            w.WriteAttributeString("defName", gene.def.defName);
            WriteSourceMod(w, gene.def);
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteStartElement("xenogenes");
        foreach (var gene in pawn.genes.Xenogenes)
        {
            if (gene?.def == null) continue;
            w.WriteStartElement("li");
            w.WriteAttributeString("defName", gene.def.defName);
            WriteSourceMod(w, gene.def);
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteEndElement(); // genes
    }

    // ── Save: Skills ──

    private static void WriteSkills(XmlWriter w, Pawn pawn)
    {
        if (pawn.skills == null) return;

        w.WriteStartElement("skills");
        foreach (var skill in pawn.skills.skills)
        {
            if (skill?.def == null) continue;
            w.WriteStartElement("li");
            w.WriteAttributeString("defName", skill.def.defName);
            w.WriteAttributeString("level", skill.levelInt.ToString());
            w.WriteAttributeString("passion", ((int)skill.passion).ToString());
            w.WriteAttributeString("passionName", skill.passion.ToString());
            w.WriteAttributeString("xpSinceLastLevel", skill.xpSinceLastLevel.ToString("F0"));
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ── Save: Hediffs ──

    private static void WriteHediffs(XmlWriter w, Pawn pawn)
    {
        if (pawn.health?.hediffSet == null) return;

        w.WriteStartElement("hediffs");
        foreach (var hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff?.def == null) continue;
            if (!hediff.def.duplicationAllowed) continue;
            // Skip non-organic implants/bionics
            if ((hediff is Hediff_AddedPart || hediff is Hediff_Implant) && !hediff.def.organicAddedBodypart) continue;

            w.WriteStartElement("li");
            w.WriteAttributeString("defName", hediff.def.defName);
            w.WriteAttributeString("severity", hediff.Severity.ToString("F3"));
            if (hediff.Part != null)
                w.WriteAttributeString("bodyPart", hediff.Part.def.defName);
            WriteSourceMod(w, hediff.def);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ── Save: Abilities ──

    private static void WriteAbilities(XmlWriter w, Pawn pawn)
    {
        if (pawn.abilities?.abilities == null) return;

        w.WriteStartElement("abilities");
        foreach (var ability in pawn.abilities.abilities)
        {
            if (ability?.def == null) continue;
            w.WriteStartElement("li");
            w.WriteAttributeString("defName", ability.def.defName);
            WriteSourceMod(w, ability.def);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ── Save: Apparel & Equipment ──

    private static void WriteApparel(XmlWriter w, Pawn pawn)
    {
        // Apparel (clothing/armor)
        if (pawn.apparel?.WornApparel != null && pawn.apparel.WornApparel.Count > 0)
        {
            w.WriteStartElement("apparel");
            foreach (var worn in pawn.apparel.WornApparel)
            {
                if (worn?.def == null) continue;
                w.WriteStartElement("li");
                w.WriteAttributeString("defName", worn.def.defName);
                WriteSourceMod(w, worn.def);
                if (worn.Stuff != null)
                    w.WriteAttributeString("stuff", worn.Stuff.defName);
                w.WriteAttributeString("hp", worn.HitPoints.ToString());
                w.WriteAttributeString("maxHp", worn.MaxHitPoints.ToString());
                if (worn.TryGetQuality(out var quality))
                    w.WriteAttributeString("quality", quality.ToString());
                var colorComp = worn.TryGetComp<CompColorable>();
                if (colorComp != null && colorComp.Active)
                {
                    var c = colorComp.Color;
                    w.WriteAttributeString("color", $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}");
                }
                if (pawn.apparel.IsLocked(worn))
                    w.WriteAttributeString("locked", "true");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        // Equipment (weapons)
        if (pawn.equipment?.AllEquipmentListForReading != null && pawn.equipment.AllEquipmentListForReading.Count > 0)
        {
            w.WriteStartElement("equipment");
            foreach (var equip in pawn.equipment.AllEquipmentListForReading)
            {
                if (equip?.def == null) continue;
                w.WriteStartElement("li");
                w.WriteAttributeString("defName", equip.def.defName);
                WriteSourceMod(w, equip.def);
                if (equip.Stuff != null)
                    w.WriteAttributeString("stuff", equip.Stuff.defName);
                w.WriteAttributeString("hp", equip.HitPoints.ToString());
                w.WriteAttributeString("maxHp", equip.MaxHitPoints.ToString());
                if (equip.TryGetQuality(out var quality))
                    w.WriteAttributeString("quality", quality.ToString());
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

    // ── Save helpers ──

    private static void WriteSimple(XmlWriter w, string name, string value)
    {
        if (value.NullOrEmpty()) return;
        w.WriteElementString(name, value);
    }

    private static void WriteColor(XmlWriter w, string name, Color c)
    {
        w.WriteStartElement(name);
        w.WriteAttributeString("r", c.r.ToString("F3"));
        w.WriteAttributeString("g", c.g.ToString("F3"));
        w.WriteAttributeString("b", c.b.ToString("F3"));
        w.WriteAttributeString("a", c.a.ToString("F3"));
        w.WriteEndElement();
    }

    private static void WriteDefWithSource(XmlWriter w, string elementName, Def def)
    {
        if (def == null) return;
        w.WriteStartElement(elementName);
        w.WriteAttributeString("defName", def.defName);
        WriteSourceMod(w, def);
        w.WriteEndElement();
    }

    /// <summary>
    /// Writes MayRequire="packageId" if the def comes from a mod (not vanilla).
    /// Uses def.modContentPack to detect source.
    /// </summary>
    private static void WriteSourceMod(XmlWriter w, Def def)
    {
        if (def?.modContentPack == null) return;
        if (def.modContentPack.IsOfficialMod || def.modContentPack.IsCoreMod) return;
        var packageId = def.modContentPack.PackageId;
        if (!packageId.NullOrEmpty())
            w.WriteAttributeString("MayRequire", packageId.ToLower());
    }

    // ═══════════════════════════════════════════════════════════════
    //  LOAD — Blueprint XML → Fresh Pawn
    // ═══════════════════════════════════════════════════════════════

    private static readonly List<string> loadWarnings = new();

    public static Pawn LoadBlueprint(string filePath)
    {
        loadWarnings.Clear();

        var doc = new XmlDocument();
        doc.Load(filePath);

        var root = doc.DocumentElement;
        if (root == null || root.Name != "PawnBlueprint")
        {
            Log.Warning("[Pawn Editor] Not a PawnBlueprint file, falling back to legacy loader.");
            return null; // Signal to caller: use legacy Scribe loader
        }

        try
        {
            var pawn = BuildPawnFromBlueprint(root);
            FlushWarnings(pawn);
            return pawn;
        }
        catch (Exception ex)
        {
            Log.Error($"[Pawn Editor] Blueprint load failed: {ex}");
            return null;
        }
    }

    private static Pawn BuildPawnFromBlueprint(XmlNode root)
    {
        // ── 1. Identity ──
        var gender = ParseEnum<Gender>(GetText(root, "gender"), Gender.Male);
        float bioAge = ParseFloat(GetAttrOrText(root, "biologicalAge"), 25f);
        float chronAge = ParseFloat(GetAttrOrText(root, "chronologicalAge"), bioAge);
        if (chronAge < bioAge) chronAge = bioAge;

        var kindDef = ResolveDef<PawnKindDef>(root, "kindDef") ?? PawnKindDefOf.Colonist;

        // Xenotype (only if Biotech active)
        XenotypeDef xenotype = null;
        if (ModsConfig.BiotechActive)
            xenotype = ResolveDef<XenotypeDef>(root, "xenotypeDef");

        // ── 2. Generate fresh pawn ──
        // Ideology: use player's primary ideo if DLC active
        Ideo ideo = null;
        if (ModsConfig.IdeologyActive && Faction.OfPlayer?.ideos?.PrimaryIdeo != null)
            ideo = Faction.OfPlayer.ideos.PrimaryIdeo;

        var request = new PawnGenerationRequest(
            kind: kindDef,
            faction: Faction.OfPlayer,
            context: PawnGenerationContext.NonPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            allowFood: true,
            allowAddictions: false,
            fixedBiologicalAge: bioAge,
            fixedChronologicalAge: chronAge,
            fixedGender: gender,
            fixedIdeo: ideo,
            forbidAnyTitle: true,
            forceNoGear: true
        );
        request.ForceNoIdeoGear = true;
        request.CanGeneratePawnRelations = false;
        if (xenotype != null)
            request.ForcedXenotype = xenotype;

        Pawn pawn = PawnGenerator.GeneratePawn(request);

        // Force gender (PawnGenerator may override fixedGender for some xenotypes)
        if (pawn.gender != gender)
            pawn.gender = gender;

        // ── 3. Apply blueprint ──
        LoadName(pawn, root);
        LoadStory(pawn, root);
        LoadTraits(pawn, root);
        LoadGenes(pawn, root);        // Genes first — they can force hair/body/skin
        LoadAppearance(pawn, root);   // Appearance after genes to override back
        LoadStyle(pawn, root);
        LoadSkills(pawn, root);
        LoadHediffs(pawn, root);
        LoadAbilities(pawn, root);
        LoadApparel(pawn, root);

        // Biotech extras
        if (ModsConfig.BiotechActive && pawn.genes != null)
        {
            var xenoName = GetText(root, "xenotypeName");
            if (!xenoName.NullOrEmpty()) pawn.genes.xenotypeName = xenoName;

            var iconDef = ResolveDef<XenotypeIconDef>(root, "xenotypeIconDef");
            if (iconDef != null) pawn.genes.iconDef = iconDef;

            var growthPts = ParseFloat(GetText(root, "growthPoints"), -1f);
            if (growthPts >= 0f) pawn.ageTracker.growthPoints = growthPts;
        }

        // Favorite color — stored as Color, need to find closest ColorDef
        var favColorNode = root.SelectSingleNode("favoriteColor");
        if (favColorNode != null && pawn.story != null)
        {
            var targetColor = ReadColor(favColorNode);
            ColorDef bestMatch = null;
            float bestDist = float.MaxValue;
            foreach (var cd in DefDatabase<ColorDef>.AllDefsListForReading)
            {
                float dist = ColorDistance(cd.color, targetColor);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestMatch = cd;
                }
            }
            if (bestMatch != null)
                pawn.story.favoriteColor = bestMatch;
        }

        // ── 4. Finalize ──
        try { pawn.Notify_DisabledWorkTypesChanged(); } catch { }
        try
        {
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
        }
        catch { }

        // Apply ideo certainty LAST (after all finalization that might reset it)
        if (ModsConfig.IdeologyActive && pawn.ideo != null)
        {
            var certNode = root.SelectSingleNode("ideoCertainty");
            if (certNode != null)
            {
                var certainty = ParseFloat(certNode.InnerText?.Trim(), 1f);
                pawn.ideo.SetIdeo(pawn.Ideo ?? ideo);
                pawn.ideo.certaintyInt = certainty;
            }
        }

        return pawn;
    }

    // ── Load: Name ──

    private static void LoadName(Pawn pawn, XmlNode root)
    {
        try
        {
            var nameNode = root.SelectSingleNode("name");
            if (nameNode == null) return;

            var first = nameNode.Attributes?["first"]?.Value;
            var nick = nameNode.Attributes?["nick"]?.Value;
            var last = nameNode.Attributes?["last"]?.Value;

            if (first != null || last != null)
                pawn.Name = new NameTriple(first ?? "", nick ?? "", last ?? "");
            else if (!nameNode.InnerText.NullOrEmpty())
                pawn.Name = NameTriple.FromString(nameNode.InnerText);
        }
        catch (Exception ex) { Warn($"Name: {ex.Message}"); }
    }

    // ── Load: Story ──

    private static void LoadStory(Pawn pawn, XmlNode root)
    {
        if (pawn.story == null) return;
        try
        {
            var childhood = ResolveDef<BackstoryDef>(root, "childhood");
            if (childhood != null) pawn.story.Childhood = childhood;

            var adulthood = ResolveDef<BackstoryDef>(root, "adulthood");
            if (adulthood != null) pawn.story.Adulthood = adulthood;
        }
        catch (Exception ex) { Warn($"Story: {ex.Message}"); }
    }

    // ── Load: Appearance ──

    private static void LoadAppearance(Pawn pawn, XmlNode root)
    {
        if (pawn.story == null) return;
        var app = root.SelectSingleNode("appearance");
        if (app == null) return;

        try
        {
            var bodyType = ResolveDef<BodyTypeDef>(app, "bodyType");
            if (bodyType != null) pawn.story.bodyType = bodyType;

            var headType = ResolveDef<HeadTypeDef>(app, "headType");
            if (headType != null) pawn.story.headType = headType;

            var hairDef = ResolveDef<HairDef>(app, "hairDef");
            if (hairDef != null) pawn.story.hairDef = hairDef;

            var furDef = ResolveDef<FurDef>(app, "furDef");
            if (furDef != null) pawn.story.furDef = furDef;

            var hairColorNode = app.SelectSingleNode("hairColor");
            if (hairColorNode != null) pawn.story.HairColor = ReadColor(hairColorNode);

            var skinBaseNode = app.SelectSingleNode("skinColorBase");
            if (skinBaseNode != null) pawn.story.SkinColorBase = ReadColor(skinBaseNode);

            var skinOverNode = app.SelectSingleNode("skinColorOverride");
            if (skinOverNode != null) pawn.story.skinColorOverride = ReadColor(skinOverNode);

            // Melanin
            var melaninStr = GetText(app, "melanin");
            if (!melaninStr.NullOrEmpty())
            {
                var melanin = ParseFloat(melaninStr, -1f);
                if (melanin >= 0f) pawn.story.melanin = melanin;
            }

            // Force graphics recalculation
            try
            {
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(pawn);
                GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            }
            catch { }
        }
        catch (Exception ex) { Warn($"Appearance: {ex.Message}"); }
    }

    // ── Load: Style ──

    private static void LoadStyle(Pawn pawn, XmlNode root)
    {
        if (pawn.style == null) return;
        var styleNode = root.SelectSingleNode("style");
        if (styleNode == null) return;

        try
        {
            var beardDef = ResolveDef<BeardDef>(styleNode, "beardDef");
            if (beardDef != null) pawn.style.beardDef = beardDef;

            if (ModsConfig.IdeologyActive)
            {
                var bodyTattoo = ResolveDef<TattooDef>(styleNode, "bodyTattoo");
                if (bodyTattoo != null) pawn.style.BodyTattoo = bodyTattoo;

                var faceTattoo = ResolveDef<TattooDef>(styleNode, "faceTattoo");
                if (faceTattoo != null) pawn.style.FaceTattoo = faceTattoo;
            }
        }
        catch (Exception ex) { Warn($"Style: {ex.Message}"); }
    }

    // ── Load: Traits ──

    private static void LoadTraits(Pawn pawn, XmlNode root)
    {
        if (pawn.story?.traits == null) return;
        var traitsNode = root.SelectSingleNode("traits");
        if (traitsNode == null) return;

        try
        {
            pawn.story.traits.allTraits.Clear();
            foreach (XmlNode li in traitsNode.SelectNodes("li"))
            {
                if (!IsAvailable(li)) continue;

                var defName = li.Attributes?["defName"]?.Value;
                if (defName.NullOrEmpty()) continue;

                var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
                if (def == null) { Warn($"Trait '{defName}' not found, skipping"); continue; }

                int degree = ParseInt(li.Attributes?["degree"]?.Value, 0);
                bool scenForced = ParseBool(li.Attributes?["scenForced"]?.Value, false);
                pawn.story.traits.GainTrait(new Trait(def, degree, scenForced));
            }
        }
        catch (Exception ex) { Warn($"Traits: {ex.Message}"); }
    }

    // ── Load: Genes ──

    private static void LoadGenes(Pawn pawn, XmlNode root)
    {
        if (!ModsConfig.BiotechActive || pawn.genes == null) return;
        var genesNode = root.SelectSingleNode("genes");
        if (genesNode == null) return;

        try
        {
            // Endogenes
            var endoNode = genesNode.SelectSingleNode("endogenes");
            if (endoNode != null)
            {
                pawn.genes.Endogenes.Clear();
                foreach (XmlNode li in endoNode.SelectNodes("li"))
                {
                    if (!IsAvailable(li)) continue;
                    var defName = li.Attributes?["defName"]?.Value;
                    if (defName.NullOrEmpty()) continue;

                    var def = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                    if (def == null) { Warn($"Endogene '{defName}' not found, skipping"); continue; }
                    pawn.genes.AddGene(def, xenogene: false);
                }
            }

            // Xenogenes
            var xenoNode = genesNode.SelectSingleNode("xenogenes");
            if (xenoNode != null)
            {
                pawn.genes.Xenogenes.Clear();
                foreach (XmlNode li in xenoNode.SelectNodes("li"))
                {
                    if (!IsAvailable(li)) continue;
                    var defName = li.Attributes?["defName"]?.Value;
                    if (defName.NullOrEmpty()) continue;

                    var def = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                    if (def == null) { Warn($"Xenogene '{defName}' not found, skipping"); continue; }
                    pawn.genes.AddGene(def, xenogene: true);
                }
            }
        }
        catch (Exception ex) { Warn($"Genes: {ex.Message}"); }
    }

    // ── Load: Skills ──

    private static void LoadSkills(Pawn pawn, XmlNode root)
    {
        if (pawn.skills == null) return;
        var skillsNode = root.SelectSingleNode("skills");
        if (skillsNode == null) return;

        try
        {
            pawn.skills.skills.Clear();
            foreach (XmlNode li in skillsNode.SelectNodes("li"))
            {
                var defName = li.Attributes?["defName"]?.Value;
                if (defName.NullOrEmpty()) continue;

                var def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                if (def == null) { Warn($"Skill '{defName}' not found, skipping"); continue; }

                var passionStr = li.Attributes?["passionName"]?.Value ?? li.Attributes?["passion"]?.Value;
                Passion passion = Passion.None;
                if (!passionStr.NullOrEmpty())
                {
                    if (Enum.TryParse<Passion>(passionStr, true, out var parsed))
                        passion = parsed;
                    else if (int.TryParse(passionStr, out var intVal))
                        passion = (Passion)intVal;
                }

                var record = new SkillRecord(pawn, def)
                {
                    levelInt = ParseInt(li.Attributes?["level"]?.Value, 0),
                    passion = passion,
                    xpSinceLastLevel = ParseFloat(li.Attributes?["xpSinceLastLevel"]?.Value, 0f),
                    xpSinceMidnight = 0f
                };
                pawn.skills.skills.Add(record);
            }
        }
        catch (Exception ex) { Warn($"Skills: {ex.Message}"); }
    }

    // ── Load: Hediffs ──

    private static void LoadHediffs(Pawn pawn, XmlNode root)
    {
        if (pawn.health?.hediffSet == null) return;
        var hediffsNode = root.SelectSingleNode("hediffs");
        if (hediffsNode == null) return;

        try
        {
            foreach (XmlNode li in hediffsNode.SelectNodes("li"))
            {
                if (!IsAvailable(li)) continue;

                var defName = li.Attributes?["defName"]?.Value;
                if (defName.NullOrEmpty()) continue;

                var def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                if (def == null) { Warn($"Hediff '{defName}' not found, skipping"); continue; }
                if (!def.duplicationAllowed) continue;

                // Body part
                BodyPartRecord part = null;
                var partDef = li.Attributes?["bodyPart"]?.Value;
                if (!partDef.NullOrEmpty())
                {
                    part = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == partDef);
                    if (part == null)
                    {
                        Warn($"Body part '{partDef}' not found for hediff '{defName}', skipping");
                        continue;
                    }
                }

                // Skip non-organic implants
                if (typeof(Hediff_AddedPart).IsAssignableFrom(def.hediffClass) && !def.organicAddedBodypart) continue;
                if (typeof(Hediff_Implant).IsAssignableFrom(def.hediffClass) && !def.organicAddedBodypart) continue;

                try
                {
                    if (pawn.health.hediffSet.hediffs.Any(h => h.def == def && h.Part == part)) continue;

                    var hediff = HediffMaker.MakeHediff(def, pawn, part);
                    var sev = ParseFloat(li.Attributes?["severity"]?.Value, -1f);
                    if (sev >= 0f) hediff.Severity = sev;
                    pawn.health.hediffSet.AddDirect(hediff);
                }
                catch (Exception ex) { Warn($"Hediff '{defName}': {ex.Message}"); }
            }
        }
        catch (Exception ex) { Warn($"Hediffs: {ex.Message}"); }
    }

    // ── Load: Abilities ──

    private static void LoadAbilities(Pawn pawn, XmlNode root)
    {
        if (pawn.abilities == null) return;
        var abNode = root.SelectSingleNode("abilities");
        if (abNode == null) return;

        try
        {
            foreach (XmlNode li in abNode.SelectNodes("li"))
            {
                if (!IsAvailable(li)) continue;

                var defName = li.Attributes?["defName"]?.Value;
                if (defName.NullOrEmpty()) continue;

                var def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
                if (def == null) { Warn($"Ability '{defName}' not found, skipping"); continue; }

                if (pawn.abilities.GetAbility(def) == null)
                    pawn.abilities.GainAbility(def);
            }
        }
        catch (Exception ex) { Warn($"Abilities: {ex.Message}"); }
    }

    // ── Load: Apparel & Equipment ──

    private static void LoadApparel(Pawn pawn, XmlNode root)
    {
        // Load apparel (clothing/armor)
        var apparelNode = root.SelectSingleNode("apparel");
        if (apparelNode != null && pawn.apparel != null)
        {
            try
            {
                foreach (XmlNode li in apparelNode.SelectNodes("li"))
                {
                    if (!IsAvailable(li)) continue;

                    var defName = li.Attributes?["defName"]?.Value;
                    if (defName.NullOrEmpty()) continue;

                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (def == null) { Warn($"Apparel '{defName}' not found, skipping"); continue; }

                    ThingDef stuffDef = null;
                    var stuffName = li.Attributes?["stuff"]?.Value;
                    if (!stuffName.NullOrEmpty())
                        stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);

                    var apparel = (Apparel)ThingMaker.MakeThing(def, stuffDef);

                    // HP
                    var hpStr = li.Attributes?["hp"]?.Value;
                    if (!hpStr.NullOrEmpty() && int.TryParse(hpStr, out var hp))
                        apparel.HitPoints = hp;

                    // Quality
                    var qualStr = li.Attributes?["quality"]?.Value;
                    if (!qualStr.NullOrEmpty() && ParseEnum<QualityCategory>(qualStr, QualityCategory.Normal) is var qual)
                    {
                        var compQuality = apparel.TryGetComp<CompQuality>();
                        compQuality?.SetQuality(qual, ArtGenerationContext.Outsider);
                    }

                    // Color
                    var colorStr = li.Attributes?["color"]?.Value;
                    if (!colorStr.NullOrEmpty())
                    {
                        var parts = colorStr.Split(',');
                        if (parts.Length >= 3)
                        {
                            var color = new Color(
                                float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                                parts.Length >= 4 ? float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : 1f
                            );
                            var colorComp = apparel.TryGetComp<CompColorable>();
                            colorComp?.SetColor(color);
                        }
                    }

                    // Locked
                    var locked = li.Attributes?["locked"]?.Value == "true";

                    pawn.apparel.Wear(apparel, dropReplacedApparel: false, locked: locked);
                }
            }
            catch (Exception ex) { Warn($"Apparel: {ex.Message}"); }
        }

        // Load equipment (weapons)
        var equipNode = root.SelectSingleNode("equipment");
        if (equipNode != null && pawn.equipment != null)
        {
            try
            {
                foreach (XmlNode li in equipNode.SelectNodes("li"))
                {
                    if (!IsAvailable(li)) continue;

                    var defName = li.Attributes?["defName"]?.Value;
                    if (defName.NullOrEmpty()) continue;

                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (def == null) { Warn($"Equipment '{defName}' not found, skipping"); continue; }

                    ThingDef stuffDef = null;
                    var stuffName = li.Attributes?["stuff"]?.Value;
                    if (!stuffName.NullOrEmpty())
                        stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);

                    var weapon = (ThingWithComps)ThingMaker.MakeThing(def, stuffDef);

                    var hpStr = li.Attributes?["hp"]?.Value;
                    if (!hpStr.NullOrEmpty() && int.TryParse(hpStr, out var hp))
                        weapon.HitPoints = hp;

                    var qualStr = li.Attributes?["quality"]?.Value;
                    if (!qualStr.NullOrEmpty() && ParseEnum<QualityCategory>(qualStr, QualityCategory.Normal) is var qual)
                    {
                        var compQuality = weapon.TryGetComp<CompQuality>();
                        compQuality?.SetQuality(qual, ArtGenerationContext.Outsider);
                    }

                    pawn.equipment.AddEquipment(weapon);
                }
            }
            catch (Exception ex) { Warn($"Equipment: {ex.Message}"); }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Core helper: MayRequire check
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if an XML element's MayRequire mod is loaded. If no MayRequire → always available.
    /// </summary>
    private static bool IsAvailable(XmlNode node)
    {
        var mayRequire = node?.Attributes?["MayRequire"]?.Value;
        if (mayRequire.NullOrEmpty()) return true; // Vanilla or no dependency

        // Check if mod is active
        return ModLister.GetActiveModWithIdentifier(mayRequire, ignorePostfix: true) != null;
    }

    /// <summary>
    /// Resolves a Def from an element with defName attribute, respecting MayRequire.
    /// Returns null if def not found or mod not loaded.
    /// </summary>
    private static T ResolveDef<T>(XmlNode parent, string elementName) where T : Def
    {
        var node = parent?.SelectSingleNode(elementName);
        if (node == null) return null;

        var defName = node.Attributes?["defName"]?.Value ?? node.InnerText?.Trim();
        if (defName.NullOrEmpty()) return null;

        // If mod is missing, still try — another mod might provide the same defName
        if (!IsAvailable(node))
        {
            var fallback = DefDatabase<T>.GetNamedSilentFail(defName);
            if (fallback != null)
            {
                Warn($"{typeof(T).Name} '{defName}' found via fallback (original mod not loaded)");
                return fallback;
            }
            Warn($"{typeof(T).Name} '{defName}' skipped — mod '{node.Attributes?["MayRequire"]?.Value}' not loaded");
            return null;
        }

        var def = DefDatabase<T>.GetNamedSilentFail(defName);
        if (def == null)
            Warn($"{typeof(T).Name} '{defName}' not found");
        return def;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Parse helpers
    // ═══════════════════════════════════════════════════════════════

    private static string GetText(XmlNode parent, string xpath)
    {
        return parent?.SelectSingleNode(xpath)?.InnerText?.Trim();
    }

    private static string GetAttrOrText(XmlNode parent, string name)
    {
        var node = parent?.SelectSingleNode(name);
        if (node == null) return null;
        return node.Attributes?["value"]?.Value ?? node.InnerText?.Trim();
    }

    private static Color ReadColor(XmlNode node)
    {
        if (node == null) return Color.white;
        float r = ParseFloat(node.Attributes?["r"]?.Value, 1f);
        float g = ParseFloat(node.Attributes?["g"]?.Value, 1f);
        float b = ParseFloat(node.Attributes?["b"]?.Value, 1f);
        float a = ParseFloat(node.Attributes?["a"]?.Value, 1f);
        return new Color(r, g, b, a);
    }

    private static float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    private static int ParseInt(string text, int fallback)
    {
        if (text.NullOrEmpty()) return fallback;
        return int.TryParse(text, out var v) ? v : fallback;
    }

    private static float ParseFloat(string text, float fallback)
    {
        if (text.NullOrEmpty()) return fallback;
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private static bool ParseBool(string text, bool fallback)
    {
        if (text.NullOrEmpty()) return fallback;
        return bool.TryParse(text, out var v) ? v : fallback;
    }

    private static T ParseEnum<T>(string text, T fallback) where T : struct
    {
        if (text.NullOrEmpty()) return fallback;
        return Enum.TryParse<T>(text, true, out var v) ? v : fallback;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Warning system
    // ═══════════════════════════════════════════════════════════════

    private static void Warn(string msg) => loadWarnings.Add(msg);

    private static void FlushWarnings(Pawn pawn)
    {
        if (loadWarnings.Count == 0) return;

        var name = pawn?.Name?.ToStringFull ?? pawn?.LabelCap ?? "unknown";
        Log.Warning($"[Pawn Editor] Blueprint loaded '{name}' with {loadWarnings.Count} adjustment(s):");
        foreach (var w in loadWarnings)
            Log.Warning($"  → {w}");

        Messages.Message(
            $"Pawn Editor: '{name}' loaded with {loadWarnings.Count} adjustment(s) — check log for details.",
            MessageTypeDefOf.CautionInput, false);
        loadWarnings.Clear();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Detect format — is this a Blueprint or legacy Scribe file?
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Quick check: does this file use the new Blueprint format?
    /// Reads only the root element name.
    /// </summary>
    public static bool IsBlueprintFile(string filePath)
    {
        try
        {
            using var reader = XmlReader.Create(filePath);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                    return reader.Name == "PawnBlueprint";
            }
        }
        catch { }
        return false;
    }
}
