﻿using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Rules;
using UnityEngine;

namespace VanillaRuleModifierAssembly.RuleSetGenerators
{
    public class MorseCodeRuleGenerator : AbstractRuleSetGenerator
    {
        public MorseCodeRuleGenerator()
        {
            var possibleWords = GetLocalizedPossibleWords(VanillaRuleModifier._modSettings?.Settings.Language);
            PossibleFrequencies = PossibleFrequencies.Distinct().ToList();
            possibleWords = possibleWords.Distinct().ToList();
            if (PossibleFrequencies.Count < NumFrequenciesUsed || possibleWords.Count < NumFrequenciesUsed)
            {
                throw new Exception("Not enough frequencies or words to satisfy desired rule set size!");
            }
        }

        private static void AddCharacters()
        {
            var CreateSignalDictionaryMethod = ReflectionHelper.GetMethod(typeof(MorseCodeComponent), "CreateSignalDictionary", 0, false);
            var AddCharacterSignalMethod = ReflectionHelper.GetMethod(typeof(MorseCodeComponent), "AddCharacterSignal", 2, false);
            if (CreateSignalDictionaryMethod == null || AddCharacterSignalMethod == null) return;

            CreateSignalDictionaryMethod.Invoke();
            AddCharacterSignalMethod.Invoke();

        }

        private static int LevenshteinDistance(string a, string b)
        {
            int lengthA = a.Length;
            int lengthB = b.Length;
            var distances = new int[lengthA + 1, lengthB + 1];
            for (int i = 0; i <= lengthA; distances[i, 0] = i++) ;
            for (int j = 0; j <= lengthB; distances[0, j] = j++) ;

            for (int i = 1; i <= lengthA; i++)
                for (int j = 1; j <= lengthB; j++)
                    distances[i, j] = Math.Min(Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1), distances[i - 1, j - 1] + (b[j - 1] == a[i - 1] ? 0 : 1));
            return distances[lengthA, lengthB];
        }

        private static int Similarity(string a, string b)
        {
            var score = LevenshteinDistance(a, b);
            for (var i = 1; i < a.Length; i++)
                score = Math.Min(score, LevenshteinDistance(a.Substring(i) + a.Substring(0, i), b));
            return score;
        }

        protected override AbstractRuleSet CreateRules(bool useDefault)
        {
            var possibleWords = GetLocalizedPossibleWords(VanillaRuleModifier._modSettings?.Settings.Language);
            var extendedWords = GetLocalizedExtendedWords(VanillaRuleModifier._modSettings?.Settings.Language);

            var dictionary = new Dictionary<int, string>();
            var freqs = new List<int>(PossibleFrequencies);
            var words = new List<string>(possibleWords);
            if (CommonReflectedTypeInfo.IsModdedSeed)
            {
                words.AddRange(extendedWords);

                freqs = freqs.OrderBy(x => rand.NextDouble()).Take(NumFrequenciesUsed).ToList();

                // Choose 8 base words
                var numBaseWords = NumFrequenciesUsed / 2;
                var chosenWords = words.OrderBy(x => rand.NextDouble()).Take(numBaseWords).ToList();
                Debug.LogFormat("[Morse Code Rule Seed Generator] + Base words: {0}", string.Join(", ", chosenWords.ToArray()));

                // Find other words that are similar to these (low cycling Levenshtein distance)
                for (var i = 0; i < numBaseWords && chosenWords.Count < NumFrequenciesUsed; i++)
                {
                    var prefix = chosenWords[i].Substring(1);
                    var toAdd = words
                        .Where(w => !chosenWords.Contains(w) && !w.EndsWith(prefix))
                        .Select(w => (new { word = w, pref = rand.NextDouble(), dist = Similarity(w, chosenWords[i]) }))
                        .ToList();
                    toAdd.Sort((a, b) =>
                    {
                        var r = a.dist - b.dist;
                        return r == 0 ? Math.Sign(a.pref - b.pref) : r;
                    });
                    var howmany = Math.Min(Math.Min(rand.Next(1, 4), NumFrequenciesUsed - chosenWords.Count), toAdd.Count);
                    Debug.LogFormat(@"[Morse Code Rule Seed Generator] From {0}, adding words: {1}", chosenWords[i], string.Join(", ", toAdd.Take(howmany).Select(w => string.Format("{0}/{1}/{2}", w.word, w.dist, w.pref)).ToArray()));
                    chosenWords.AddRange(toAdd.Take(howmany).Select(inf => inf.word));
                }
                dictionary = Enumerable.Range(0, NumFrequenciesUsed).ToDictionary(i => freqs[i], i => GetTermForWord(chosenWords[i]));
            }
            else
            {
                for (var i = 0; i < NumFrequenciesUsed; i++)
                {
                    var ix = rand.Next(0, freqs.Count);
                    var freq = freqs[ix];
                    freqs.RemoveAt(ix);
                    ix = rand.Next(0, words.Count);
                    var value = words[ix];
                    words.RemoveAt(ix);
                    dictionary.Add(freq, GetTermForWord(value));
                }
            }
            var ruleSet = new MorseCodeRuleSet(dictionary);
            var morseCodes = ".-,-...,-.-.,-..,.,..-.,--.,....,..,.---,-.-,.-..,--,-.,---,.--.,--.-,.-.,...,-,..-,...-,.--,-..-,-.--,--..".Split(',');
            ruleSet.SignalDict = new Dictionary<char, List<MorseCodeComponent.SignalEnum>>();
            for (int i = 0; i < 26; i++)
                ruleSet.SignalDict[(char) ('a' + i)] = morseCodes[i].Select(ch => ch == '.' ? MorseCodeComponent.SignalEnum.Dot : MorseCodeComponent.SignalEnum.Dash).ToList();
            return ruleSet;
        }

        private string GetTermForWord(string word)
        {
            var term = $"mod/VanillaRuleModifier_MorseCodeWord_{word}";
            Localization.AddModTermData(term, word);

            return term;
        }

        public MorseCodeRuleSet GenerateMorseCodeRuleSet(int seed)
        {
            return (MorseCodeRuleSet) GenerateRuleSet(seed);
        }

        protected static readonly int NumFrequenciesUsed = 16;
        protected List<int> PossibleFrequencies = new List<int>
        {
            502, 505, 512, 515,
            522, 525, 532, 535,
            542, 545, 552, 555,
            562, 565, 572, 575,
            582, 585, 592, 595,
            600
        };

        protected List<string> GetLocalizedPossibleWords(string language)
        {
            return language != null && LocalizedPossibleWords.ContainsKey(language)
                ? LocalizedPossibleWords[language]
                : LocalizedPossibleWords["en"];
        }

        protected List<string> GetLocalizedExtendedWords(string language)
        {
            return language != null && LocalizedExtendedWords.ContainsKey(language)
                ? LocalizedExtendedWords[language]
                : LocalizedExtendedWords["en"];
        }

        protected static Dictionary<string, List<string>> LocalizedPossibleWords = new Dictionary<string, List<string>>
        {
            {
                "en", new List<string>
                {
                    "trick", "bravo", "vector", "brain",
                    "boxes", "alien", "beats", "bombs",
                    "sting", "steak", "leaks", "verse",
                    "brick", "break", "hello", "halls",
                    "shell", "bistro", "strobe", "slick",
                    "flick"
                }
            }
        };

        protected static Dictionary<string, List<string>> LocalizedExtendedWords = new Dictionary<string, List<string>>
        {
            {
                "en", new List<string>
                {
                    // 5-letter words from vanilla Passwords
                    "there", "which", "their", "other", "about", "these", "would",
                    "write", "could", "first", "water", "sound", "place", "after",
                    "thing", "think", "great", "where", "right", "three", "small",
                    "large", "again", "spell", "house", "point", "found", "study",
                    "still", "learn", "world", "every", "below", "plant", "never",

                    // Moar 5-letter words (Passwords)
                    "aback", "abbey", "abbot", "above", "abuse", "acids", "acres", "acted", "actor", "acute", "adapt", "added", "admit", "adopt", "adult", "agent", "agony", "agree",
                    "ahead", "aided", "aimed", "aisle", "alarm", "album", "alert", "algae", "alike", "alive", "alley", "allow", "alloy", "alone", "along", "aloof", "aloud",
                    "alpha", "altar", "alter", "amend", "amino", "among", "ample", "angel", "anger", "angle", "angry", "ankle", "apart", "apple", "apply", "apron", "areas", "arena",
                    "argue", "arise", "armed", "aroma", "arose", "array", "arrow", "arson", "ashes", "aside", "asked", "assay", "asset", "atoms", "attic", "audio", "audit", "avoid",
                    "await", "awake", "award", "aware", "awful", "awoke", "backs", "bacon", "badge", "badly", "baked", "baker", "balls", "bands", "banks", "barge", "baron", "basal",
                    "based", "bases", "basic", "basin", "basis", "batch", "baths", "beach", "beads", "beams", "beans", "beard", "bears", "beast", "beech", "beers", "began", "begin",
                    "begun", "being", "bells", "belly", "belts", "bench", "bible", "bikes", "bills", "birds", "birth", "black", "blade", "blame", "bland", "blank", "blast",
                    "blaze", "bleak", "bleat", "blend", "bless", "blind", "block", "bloke", "blond", "blood", "bloom", "blown", "blows", "blues", "blunt", "board", "boats", "bogus",
                    "bolts", "bonds", "bones", "bonus", "books", "boost", "boots", "bored", "borne", "bound", "bowed", "bowel", "bowls", "boxed", "boxer",
                    "brake", "brand", "brass", "brave", "bread", "bream", "breed", "bride", "brief", "bring", "brink", "brisk", "broad", "broke", "broom", "brown",
                    "brows", "brush", "build", "built", "bulbs", "bulky", "bulls", "bunch", "bunny", "burns", "burnt", "burst", "buses", "buyer", "cabin", "cable", "cache", "cakes",
                    "calls", "camps", "canal", "candy", "canoe", "canon", "cards", "cared", "carer", "cares", "cargo", "carry", "cases", "catch", "cater", "cause", "caves", "cease",
                    "cells", "cents", "chain", "chair", "chalk", "chaos", "chaps", "charm", "chart", "chase", "cheap", "check", "cheek", "cheer", "chess", "chest", "chief", "child",
                    "chill", "china", "chips", "choir", "chord", "chose", "chunk", "cider", "cigar", "cited", "cites", "civic", "civil", "claim", "clash", "class", "claws", "clean",
                    "clear", "clerk", "click", "cliff", "climb", "cloak", "clock", "close", "cloth", "cloud", "clown", "clubs", "cluck", "clues", "clung", "coach", "coast", "coats",
                    "cocoa", "codes", "coins", "colon", "comes", "comic", "coral", "corps", "costs", "couch", "cough", "count", "court", "cover", "crack", "craft", "crane", "crash",
                    "crate", "crazy", "cream", "creed", "crept", "crest", "crews", "cried", "cries", "crime", "crisp", "crops", "cross", "crowd", "crown", "crude", "cruel", "crust",
                    "crypt", "cubic", "curls", "curly", "curry", "curse", "curve", "cycle", "daddy", "daily", "dairy", "dance", "dared", "dated", "dates", "deals", "dealt", "death",
                    "debit", "debts", "debut", "decay", "decor", "decoy", "deeds", "deity", "delay", "dense", "depot", "depth", "derby", "derry", "desks", "deter", "devil", "diary",
                    "diets", "dimly", "dirty", "disco", "discs", "disks", "ditch", "dived", "dizzy", "docks", "dodgy", "doing", "dolls", "donor", "doors", "doses", "doubt", "dough",
                    "downs", "dozen", "draft", "drain", "drama", "drank", "drawn", "draws", "dread", "dream", "dress", "dried", "drift", "drill", "drily", "drink", "drive", "drops",
                    "drove", "drown", "drugs", "drums", "drunk", "duchy", "ducks", "dunes", "dusty", "dutch", "dwarf", "dying", "eager", "eagle", "early", "earth", "eased", "eaten",
                    "edges", "eerie", "eight", "elbow", "elder", "elect", "elite", "elves", "empty", "ended", "enemy", "enjoy", "enter", "entry", "envoy", "equal", "erect", "error",
                    "essay", "ethos", "event", "exact", "exams", "exert", "exile", "exist", "extra", "faced", "faces", "facts", "faded", "fails", "faint", "fairs", "fairy", "faith",
                    "falls", "false", "famed", "fancy", "fares", "farms", "fatal", "fatty", "fault", "fauna", "fears", "feast", "feels", "fella", "fence", "feret", "ferry", "fetal",
                    "fetch", "fever", "fewer", "fibre", "field", "fiery", "fifth", "fifty", "fight", "filed", "files", "fills", "films", "final", "finds", "fined", "finer", "fines",
                    "fired", "fires", "firms", "fists", "fiver", "fixed", "flags", "flair", "flame", "flank", "flash", "flask", "flats", "flaws", "fleet", "flesh", "flies", "float",
                    "flock", "flood", "floor", "flora", "flour", "flown", "flows", "fluid", "flung", "flunk", "flush", "flute", "focal", "focus", "folds", "folks", "folly", "fonts",
                    "foods", "fools", "force", "forms", "forth", "forty", "forum", "fours", "foxes", "foyer", "frail", "frame", "franc", "frank", "fraud", "freak", "freed", "fresh",
                    "fried", "frogs", "front", "frost", "frown", "froze", "fruit", "fuels", "fully", "fumes", "funds", "funny", "gains", "games", "gangs", "gases", "gates", "gauge",
                    "gazed", "geese", "genes", "genre", "genus", "ghost", "giant", "gifts", "girls", "given", "gives", "glare", "glass", "gleam", "globe", "gloom", "glory", "gloss",
                    "glove", "goals", "goats", "going", "goods", "goose", "gorge", "grace", "grade", "grain", "grand", "grant", "graph", "grasp", "grass", "grave", "greed", "greek",
                    "green", "greet", "grief", "grill", "grips", "groom", "gross", "group", "grown", "grows", "guard", "guess", "guest", "guide", "guild", "guilt", "guise",
                    "gulls", "gully", "gypsy", "habit", "hairs", "hairy", "hands", "handy", "hangs", "happy", "hardy", "harsh", "haste", "hasty", "hatch", "hated", "hates",
                    "haven", "havoc", "heads", "heady", "heard", "hears", "heart", "heath", "heavy", "hedge", "heels", "hefty", "heirs", "helps", "hence", "henry", "herbs",
                    "herds", "hills", "hints", "hired", "hobby", "holds", "holes", "holly", "homes", "honey", "hooks", "hoped", "hopes", "horns", "horse", "hosts", "hotel", "hours",
                    "human", "humus", "hurry", "hurts", "hymns", "icing", "icons", "ideal", "ideas", "idiot", "image", "imply", "index", "india", "inert", "infer", "inner", "input",
                    "irony", "issue", "items", "ivory", "japan", "jeans", "jelly", "jewel", "joins", "joint", "joker", "jokes", "jolly", "joule", "joust", "judge", "juice",
                    "keeps", "kicks", "kills", "kinds", "kings", "knees", "knelt", "knife", "knobs", "knock", "knots", "known", "knows", "label", "lacks", "lager", "lakes", "lambs",
                    "lamps", "lands", "lanes", "laser", "lasts", "later", "laugh", "lawns", "layer", "leads", "leant", "leapt", "lease", "least", "leave", "ledge", "legal", "lemon",
                    "level", "lever", "libel", "lifts", "light", "liked", "likes", "limbs", "limit", "lined", "linen", "liner", "lines", "links", "lions", "lists", "litre", "lived",
                    "liver", "lives", "loads", "loans", "lobby", "local", "locks", "locus", "lodge", "lofty", "logic", "looks", "loops", "loose", "lords", "lorry", "loser", "loses",
                    "lotus", "loved", "lover", "loves", "lower", "loyal", "lucky", "lumps", "lunch", "lungs", "lying", "macho", "madam", "magic", "mains", "maize", "major", "maker",
                    "makes", "males", "manor", "march", "marks", "marry", "marsh", "masks", "match", "mates", "maths", "maybe", "mayor", "meals", "means", "meant", "medal", "media",
                    "meets", "menus", "mercy", "merge", "merit", "merry", "messy", "metal", "meter", "metre", "micro", "midst", "might", "miles", "mills", "minds", "miner", "mines",
                    "minor", "minus", "misty", "mixed", "model", "modem", "modes", "moist", "moles", "money", "monks", "month", "moods", "moors", "moral", "motif", "motor", "motto",
                    "mould", "mound", "mount", "mouse", "mouth", "moved", "moves", "movie", "muddy", "mummy", "mused", "music", "myths", "nails", "naive", "named", "names",
                    "nanny", "nasty", "naval", "necks", "needs", "nerve", "nests", "newer", "newly", "nicer", "niche", "niece", "night", "ninth", "noble", "nodes", "noise",
                    "noisy", "nomes", "norms", "north", "noses", "noted", "notes", "novel", "nurse", "nutty", "nylon", "occur", "ocean", "oddly", "odour", "offer", "often", "older",
                    "olive", "onion", "onset", "opens", "opera", "orbit", "order", "organ", "ought", "ounce", "outer", "overs", "overt", "owned", "owner", "oxide", "ozone", "packs",
                    "pages", "pains", "paint", "pairs", "palms", "panel", "panic", "pants", "papal", "paper", "parks", "parts", "party", "pasta", "paste", "patch", "paths", "patio",
                    "pause", "peace", "peaks", "pearl", "pears", "peers", "penal", "pence", "penny", "pests", "petty", "phase", "phone", "photo", "piano", "picks", "piece", "piers",
                    "piled", "piles", "pills", "pilot", "pinch", "pints", "pious", "pipes", "pitch", "pizza", "plain", "plane", "plans", "plate", "plays", "plead", "pleas", "plots",
                    "plump", "poems", "poets", "polar", "poles", "polls", "ponds", "pools", "porch", "pores", "ports", "posed", "poses", "posts", "pound", "power", "press", "price",
                    "pride", "prime", "print", "prior", "privy", "prize", "probe", "prone", "proof", "prose", "proud", "prove", "proxy", "pulls", "pulse", "pumps", "punch", "pupil",
                    "puppy", "purse", "quack", "queen", "query", "quest", "queue", "quick", "quiet", "quite", "quota", "quote", "raced", "races", "radar", "radio", "raids", "rails",
                    "raise", "rally", "range", "ranks", "rapid", "rated", "rates", "ratio", "razor", "reach", "react", "reads", "ready", "realm", "rebel", "refer", "reign",
                    "reins", "relax", "remit", "renal", "renew", "rents", "repay", "reply", "resin", "rests", "rider", "ridge", "rifle", "rigid", "rings", "riots", "risen", "rises",
                    "risks", "risky", "rites", "rival", "river", "roads", "robes", "robot", "rocks", "rocky", "rogue", "roles", "rolls", "roman", "roofs", "rooms", "roots", "ropes",
                    "roses", "rotor", "rouge", "rough", "round", "route", "rover", "royal", "rugby", "ruins", "ruled", "ruler", "rules", "rural", "rusty", "sadly", "safer", "sails",
                    "saint", "salad", "sales", "salon", "salts", "sands", "sandy", "satin", "sauce", "saved", "saves", "scale", "scalp", "scant", "scarf", "scars", "scene", "scent",
                    "scoop", "scope", "score", "scots", "scrap", "screw", "scrum", "seals", "seams", "seats", "seeds", "seeks", "seems", "seize", "sells", "sends", "sense", "serum",
                    "serve", "seven", "sexes", "shade", "shady", "shaft", "shake", "shaky", "shall", "shame", "shape", "share", "sharp", "sheep", "sheer", "sheet", "shelf",
                    "shift", "shiny", "ships", "shire", "shirt", "shock", "shoes", "shone", "shook", "shoot", "shops", "shore", "short", "shots", "shout", "shown", "shows", "shrug",
                    "sides", "siege", "sight", "signs", "silly", "since", "sings", "sites", "sixth", "sixty", "sizes", "skies", "skill", "skins", "skirt", "skull", "slabs", "slate",
                    "slave", "sleek", "sleep", "slept", "slice", "slide", "slope", "slots", "slump", "smart", "smell", "smile", "smoke", "snake", "sober", "socks", "soils", "solar",
                    "solid", "solve", "songs", "sorry", "sorts", "souls", "south", "space", "spade", "spare", "spark", "spate", "spawn", "speak", "speed", "spend", "spent", "spies",
                    "spine", "splat", "split", "spoil", "spoke", "spoon", "sport", "spots", "spray", "spurs", "squad", "stack", "staff", "stage", "stain", "stair", "stake", "stale",
                    "stall", "stamp", "stand", "stare", "stark", "stars", "start", "state", "stays", "steal", "steam", "steel", "steep", "steer", "stems", "steps", "stern",
                    "stick", "stiff", "stock", "stole", "stone", "stony", "stood", "stool", "stops", "store", "storm", "story", "stout", "stove", "strap", "straw", "stray",
                    "strip", "stuck", "stuff", "style", "suede", "sugar", "suite", "suits", "sunny", "super", "surge", "swans", "swear", "sweat", "sweep", "sweet", "swept", "swift",
                    "swing", "swiss", "sword", "swore", "sworn", "swung", "table", "tacit", "tails", "taken", "takes", "tales", "talks", "tanks", "tapes", "tasks", "taste", "tasty",
                    "taxed", "taxes", "taxis", "teach", "teams", "tears", "teddy", "teens", "teeth", "tells", "telly", "tempo", "tends", "tenor", "tense", "tenth", "tents", "terms",
                    "tests", "texas", "texts", "thank", "theft", "theme", "thick", "thief", "thigh", "third", "those", "threw", "throw", "thumb", "tidal", "tides", "tiger", "tight",
                    "tiles", "times", "timid", "tired", "title", "toast", "today", "token", "tones", "tonic", "tonne", "tools", "toons", "tooth", "topic", "torch", "total", "touch",
                    "tough", "tours", "towel", "tower", "towns", "toxic", "trace", "track", "tract", "trade", "trail", "train", "trait", "tramp", "trams", "trays", "treat", "trees",
                    "trend", "trial", "tribe", "tried", "tries", "trips", "troop", "trout", "truce", "truck", "truly", "trunk", "trust", "truth", "tubes", "tummy", "tunes",
                    "tunic", "turks", "turns", "tutor", "twice", "twins", "twist", "tying", "types", "tyres", "ulcer", "unban", "uncle", "under", "undue", "unfit", "union", "unite",
                    "units", "unity", "until", "upper", "upset", "urban", "urged", "urine", "usage", "users", "using", "usual", "utter", "vague", "valid", "value", "valve", "vault",
                    "veins", "venue", "verbs", "verge", "vicar", "video", "views", "villa", "vines", "vinyl", "virus", "visit", "vital", "vivid", "vocal", "vodka", "voice",
                    "voted", "voter", "votes", "vowed", "vowel", "wages", "wagon", "waist", "waits", "walks", "walls", "wants", "wards", "wares", "warns", "waste", "watch", "waved",
                    "waves", "wears", "weary", "wedge", "weeds", "weeks", "weigh", "weird", "wells", "welsh", "whale", "wheat", "wheel", "while", "white", "whole", "whose", "widen",
                    "wider", "widow", "width", "wills", "winds", "windy", "wines", "wings", "wiped", "wires", "wiser", "witch", "witty", "wives", "woken", "woman", "women", "woods",
                    "words", "works", "worms", "worry", "worse", "worst", "worth", "wound", "woven", "wrath", "wreck", "wrist", "wrong", "wrote", "wryly", "xerox", "yacht", "yards",
                    "yawns", "years", "yeast", "yield", "young", "yours", "youth", "zilch", "zones",

                    // Moar 6-letter words
                    "aboard", "abroad", "absorb", "abused", "abuses", "accent", "accept", "access", "accord", "across", "acting", "action", "actors", "adding", "adjust", "admire", "admits", "adults", "advent",
                    "advert", "advice", "advise", "affair", "affect", "afford", "afield", "ageing", "agency", "agenda", "agents", "agreed", "agrees", "aiming", "albeit", "albums", "aliens", "allies", "allows",
                    "almost", "always", "amidst", "amount", "amused", "anchor", "angels", "angles", "animal", "ankles", "answer", "anyhow", "anyone", "anyway", "appeal", "appear", "apples", "arches", "argued",
                    "argues", "arisen", "arises", "armies", "armour", "around", "arrest", "arrive", "arrows", "artery", "artist", "ascent", "ashore", "asking", "aspect", "assent", "assert", "assess", "assets",
                    "assign", "assist", "assume", "assure", "asthma", "asylum", "attach", "attack", "attain", "attend", "author", "autumn", "avenue", "avoids", "awards", "babies", "backed", "ballet", "ballot",
                    "banana", "banged", "banker", "banned", "banner", "barely", "barley", "barman", "barons", "barrel", "basics", "basins", "basket", "battle", "beasts", "beaten", "beauty", "became", "become",
                    "before", "begged", "begins", "behalf", "behave", "behind", "beings", "belief", "belong", "beside", "better", "beware", "beyond", "bidder", "bigger", "biopsy", "births", "bishop", "biting",
                    "bitten", "blacks", "blades", "blamed", "blocks", "blokes", "bloody", "blouse", "boards", "boasts", "bodies", "boiler", "boldly", "bomber", "bonnet", "booked", "border", "borrow", "bosses",
                    "bother", "bottle", "bottom", "bought", "bounds", "bowler", "boxing", "brains", "brakes", "branch", "brands", "brandy", "breach", "breaks", "breast", "breath", "breeds", "breeze", "bricks",
                    "bridge", "brings", "broken", "broker", "bronze", "bubble", "bucket", "budget", "buffer", "buffet", "bugger", "builds", "bullet", "bundle", "burden", "bureau", "burial", "buried", "burned",
                    "burrow", "bursts", "bushes", "butler", "butter", "button", "buyers", "buying", "bypass", "cables", "called", "caller", "calmly", "calves", "camera", "campus", "canals", "cancel", "cancer",
                    "candle", "cannon", "canopy", "canvas", "carbon", "career", "carers", "caring", "carpet", "carrot", "carved", "castle", "cattle", "caught", "caused", "causes", "cavity", "ceased", "cellar",
                    "cement", "census", "center", "centre", "cereal", "chains", "chairs", "chance", "change", "chapel", "charge", "charts", "checks", "cheeks", "cheers", "cheese", "cheque", "cherry", "chicks",
                    "chiefs", "choice", "choose", "chords", "chorus", "chosen", "chunks", "church", "cinema", "circle", "circus", "cities", "citing", "claims", "clause", "clergy", "clerks", "client", "cliffs",
                    "climax", "clinic", "clocks", "clones", "closed", "closer", "closes", "clouds", "clutch", "coasts", "coffee", "coffin", "cohort", "colder", "coldly", "collar", "colony", "colour", "column",
                    "combat", "comedy", "coming", "commit", "comply", "convey", "convoy", "cooked", "cooker", "cooler", "coolly", "copied", "copies", "coping", "copper", "corner", "corpse", "corpus", "cortex",
                    "cotton", "counts", "county", "couple", "coupon", "course", "courts", "cousin", "covers", "cracks", "cradle", "create", "credit", "crimes", "crises", "crisis", "crisps", "critic", "crowds",
                    "cruise", "crying", "cuckoo", "curled", "cursed", "cursor", "curves", "custom", "cutter", "cycles", "damage", "danced", "dancer", "dances", "danger", "daring", "darker", "dashed", "dating",
                    "dealer", "dearly", "deaths", "debate", "debris", "debtor", "decade", "decide", "decree", "deemed", "deeper", "deeply", "defeat", "defect", "defend", "define", "degree", "delays", "demand",
                    "demise", "demons", "denial", "denied", "denies", "depend", "depths", "deputy", "derive", "desert", "design", "desire", "detail", "detect", "device", "devise", "devote", "diesel", "differ",
                    "digest", "digits", "dinghy", "dining", "dinner", "direct", "dishes", "dismay", "divers", "divert", "divide", "diving", "doctor", "dollar", "domain", "donkey", "donors", "doomed", "double",
                    "doubly", "doubts", "dozens", "dragon", "drains", "drawer", "dreams", "drinks", "driven", "driver", "drives", "drying", "dumped", "during", "duties", "eagles", "earned", "easier", "easily",
                    "easing", "easter", "eating", "echoed", "echoes", "edited", "editor", "effect", "effort", "eighth", "eighty", "either", "elbows", "elders", "eldest", "eleven", "elites", "embark", "embryo",
                    "emerge", "empire", "employ", "enable", "enamel", "ending", "endure", "energy", "engage", "engine", "enjoys", "enough", "ensure", "entail", "enters", "entity", "enzyme", "equals", "equity",
                    "eroded", "errors", "escape", "escort", "essays", "estate", "esteem", "ethics", "evenly", "events", "evolve", "exceed", "except", "excess", "excuse", "exists", "expand", "expect", "expert",
                    "expiry", "export", "expose", "extend", "extent", "extras", "fabric", "facets", "facing", "factor", "fading", "failed", "fairly", "fallen", "family", "famine", "farmer", "faster", "father",
                    "faults", "favour", "feared", "fellow", "female", "fences", "fibres", "fields", "fights", "figure", "filled", "filter", "finale", "finals", "finely", "finest", "finger", "finish", "firing",
                    "firmly", "fitted", "fixing", "flames", "flanks", "flatly", "flight", "flocks", "floods", "floors", "flowed", "flower", "fluids", "flurry", "flying", "folded", "folder", "follow", "forced",
                    "forces", "forest", "forget", "forgot", "format", "formed", "former", "fossil", "foster", "fought", "fourth", "frames", "francs", "freely", "freeze", "french", "frenzy", "fridge", "friend",
                    "fright", "fringe", "fronts", "frozen", "fruits", "fulfil", "fuller", "funded", "fungus", "fusion", "future", "gained", "galaxy", "gallon", "gamble", "garage", "garden", "garlic", "gasped",
                    "gather", "gazing", "geared", "gender", "genius", "gently", "gentry", "german", "ghosts", "giants", "giving", "gladly", "glance", "glands", "glared", "glider", "gloves", "golfer", "gospel",
                    "gossip", "govern", "grades", "grains", "granny", "grants", "grapes", "graphs", "gravel", "graves", "grease", "greens", "grimly", "groove", "ground", "groups", "growth", "guards", "guests",
                    "guided", "guides", "guitar", "gunmen", "gutter", "habits", "halted", "halves", "hamlet", "hammer", "handed", "handle", "happen", "harder", "hardly", "hassle", "hatred", "hauled", "having",
                    "hazard", "headed", "header", "health", "hearth", "hearts", "heater", "heaved", "heaven", "hedges", "height", "helmet", "helped", "helper", "heroes", "hidden", "hiding", "higher",
                    "highly", "hinder", "hissed", "hockey", "holder", "homage", "honour", "hoping", "horror", "horses", "hostel", "hotels", "hounds", "housed", "houses", "hugely", "hugged", "humans",
                    "humour", "hunger", "hunter", "hurdle", "ideals", "ignore", "images", "impact", "import", "impose", "inches", "income", "indeed", "induce", "infant", "influx", "inform", "injury",
                    "inland", "inputs", "insect", "inside", "insist", "insult", "insure", "intake", "intend", "intent", "invent", "invest", "invite", "island", "issued", "issues", "itself", "jacket", "jailed",
                    "jargon", "jerked", "jersey", "jewels", "jockey", "joined", "joints", "joking", "judged", "judges", "jumble", "jumped", "jumper", "jungle", "keenly", "keeper", "kettle", "kicked", "kidney",
                    "killed", "killer", "kindly", "kissed", "kisses", "knight", "knives", "labels", "labour", "lacked", "ladder", "ladies", "landed", "larger", "larvae", "lashes", "lasted", "lastly", "lately",
                    "latest", "latter", "laughs", "launch", "lawyer", "layers", "laying", "layout", "leader", "league", "leaned", "learns", "learnt", "leases", "leaves", "legacy", "legend", "legion", "lender",
                    "length", "lenses", "lesion", "lesson", "letter", "levels", "licked", "lifted", "lights", "liking", "limits", "lining", "linked", "liquid", "liquor", "listed", "listen", "litres", "litter",
                    "little", "living", "loaded", "locals", "locate", "locked", "lodged", "longed", "longer", "looked", "losers", "losing", "losses", "louder", "loudly", "lounge", "lovers", "loving", "lowest",
                    "luxury", "lyrics", "magnet", "mainly", "makers", "making", "malice", "mammal", "manage", "manner", "mantle", "manual", "marble", "margin", "marked", "marker", "market", "marrow", "masses",
                    "master", "matrix", "matter", "meadow", "medals", "medium", "melody", "member", "memory", "menace", "merely", "merger", "merits", "metals", "method", "metres", "midday", "middle", "mildly",
                    "miners", "mining", "minute", "mirror", "misery", "missed", "misses", "misuse", "mixing", "moaned", "models", "modify", "module", "moment", "monies", "monkey", "months", "morale", "morals",
                    "mortar", "mosaic", "mosque", "mostly", "mother", "motifs", "motion", "motive", "motors", "mouths", "movies", "moving", "mucosa", "murder", "murmur", "muscle", "museum", "muster", "myriad",
                    "myself", "namely", "nation", "nature", "nearby", "nearer", "nearly", "neatly", "needed", "needle", "nephew", "nerves", "newest", "nicely", "nights", "ninety", "nobles", "nobody", "nodded",
                    "noises", "notice", "notify", "noting", "notion", "nought", "novels", "novice", "nuclei", "number", "nurses", "object", "obtain", "occupy", "occurs", "oceans", "offers", "office", "offset",
                    "oldest", "onions", "opened", "opener", "openly", "operas", "oppose", "opting", "option", "orange", "ordeal", "orders", "organs", "origin", "others", "outfit", "outing", "outlet", "output",
                    "outset", "owners", "oxygen", "packed", "packet", "palace", "panels", "papers", "parade", "parcel", "pardon", "parent", "parish", "parity", "parked", "parrot", "parted", "partly", "passed",
                    "passes", "pastry", "patent", "patrol", "patron", "patted", "paused", "paving", "payers", "paying", "pearls", "peered", "pencil", "people", "pepper", "period", "permit", "person", "petals",
                    "petrol", "phases", "phoned", "phones", "photos", "phrase", "picked", "picnic", "pieces", "pigeon", "pillar", "pillow", "pilots", "pirate", "pistol", "placed", "places", "plague", "plains",
                    "planes", "planet", "plants", "plaque", "plasma", "plates", "played", "player", "please", "pledge", "plenty", "plight", "pocket", "poetry", "points", "poison", "police", "policy", "polish",
                    "pollen", "ponies", "poorer", "poorly", "popped", "portal", "porter", "posing", "posted", "poster", "potato", "pounds", "poured", "powder", "powers", "praise", "prayed", "prayer", "prefer",
                    "pretty", "priced", "prices", "priest", "prince", "prints", "prison", "prizes", "probes", "profit", "proved", "proves", "public", "pulled", "pulses", "punish", "pupils", "purely", "purity",
                    "pursue", "pushed", "puzzle", "pylori", "quarry", "quotas", "quoted", "quotes", "rabbit", "racing", "racism", "racist", "racket", "radios", "radius", "raised", "raises", "ranged", "ranges",
                    "rarely", "rarity", "rather", "rating", "ratios", "reader", "really", "reason", "rebels", "recall", "recipe", "reckon", "record", "rector", "reduce", "refers", "reflex", "reflux", "reform",
                    "refuge", "refuse", "regain", "regard", "regime", "region", "regret", "reject", "relate", "relics", "relied", "relief", "relies", "remain", "remark", "remedy", "remind", "remove", "render",
                    "rental", "repaid", "repair", "repeat", "replay", "report", "rescue", "resign", "resist", "resort", "rested", "result", "resume", "retain", "retina", "retire", "return", "reveal", "revert",
                    "review", "revise", "revive", "revolt", "reward", "rhythm", "ribbon", "richer", "richly", "ridden", "riders", "ridges", "riding", "rifles", "rights", "ripped", "rising", "ritual", "rivals",
                    "rivers", "roared", "robots", "rocket", "rolled", "roller", "romans", "rooted", "rounds", "routes", "rubbed", "rubber", "rubble", "ruined", "rulers", "ruling", "rumour", "runner", "runway",
                    "rushed", "sacked", "saddle", "safely", "safest", "safety", "sailed", "sailor", "saints", "salads", "salary", "salmon", "saloon", "sample", "saucer", "saving", "saying", "scales", "scenes",
                    "scheme", "school", "scored", "scorer", "scores", "scouts", "scraps", "scream", "screen", "screws", "script", "sealed", "seamen", "search", "season", "seated", "second", "secret", "sector",
                    "secure", "seeing", "seemed", "seized", "seldom", "select", "seller", "senate", "sensed", "senses", "series", "sermon", "served", "server", "serves", "settee", "settle", "sevens", "sewage",
                    "sewing", "sexism", "sexist", "shades", "shadow", "shafts", "shaken", "shaped", "shapes", "shared", "shares", "sheets", "shells", "sherry", "shield", "shifts", "shirts", "shocks", "shores",
                    "shorts", "should", "shouts", "showed", "shower", "shrine", "shrubs", "sighed", "sights", "signal", "signed", "silver", "simply", "singer", "sipped", "sister", "sketch", "skills", "skirts",
                    "slaves", "sleeve", "slices", "slides", "slogan", "slopes", "slowed", "slower", "slowly", "smells", "smiled", "smiles", "smoked", "snakes", "soccer", "socket", "sodium", "soften", "softer",
                    "softly", "solely", "solids", "solved", "sooner", "sorrow", "sorted", "sought", "sounds", "source", "soviet", "spaces", "spared", "speaks", "speech", "speeds", "spells", "spends", "sphere",
                    "spider", "spines", "spiral", "spirit", "splash", "spoken", "sponge", "sports", "spouse", "sprang", "spread", "spring", "squads", "square", "squash", "stable", "staged", "stages", "stairs",
                    "stakes", "stalls", "stamps", "stance", "stands", "staple", "stared", "starts", "stated", "states", "statue", "status", "stayed", "stench", "stereo", "sticks", "stitch", "stocks", "stolen",
                    "stones", "stored", "stores", "storey", "storms", "strain", "strand", "straps", "strata", "streak", "stream", "street", "stress", "stride", "strike", "string", "strips", "strode", "stroke",
                    "stroll", "struck", "studio", "styles", "submit", "suburb", "sucked", "suffer", "suited", "summed", "summer", "summit", "summon", "sunset", "supper", "supply", "surely", "survey", "sweets",
                    "switch", "swords", "symbol", "syntax", "system", "tables", "tablet", "tackle", "tactic", "tailor", "taking", "talent", "talked", "taller", "tangle", "tanker", "tapped", "target", "tariff",
                    "tarmac", "tasted", "tastes", "taught", "teased", "temper", "temple", "tenant", "tended", "tennis", "tenure", "termed", "terror", "tested", "thanks", "theirs", "themes", "theory", "theses",
                    "thesis", "thighs", "things", "thinks", "thinly", "thirds", "thirty", "though", "thread", "threat", "thrill", "throat", "throne", "thrown", "throws", "thrust", "ticket", "tigers", "tights",
                    "tiller", "timber", "timing", "tipped", "tissue", "titles", "toilet", "tokens", "tomato", "tongue", "tonnes", "topics", "topped", "tories", "torque", "tossed", "toward", "towels", "towers",
                    "traced", "traces", "tracks", "tracts", "trader", "trades", "trains", "traits", "trauma", "travel", "treats", "treaty", "trench", "trends", "trials", "tribes", "tricks", "troops", "trophy",
                    "trucks", "trusts", "truths", "trying", "tucked", "tugged", "tumour", "tunnel", "turkey", "turned", "tutors", "twelve", "twenty", "typing", "ulcers", "unduly", "unease", "unions", "united",
                    "unless", "unlike", "unrest", "update", "upheld", "upland", "uptake", "urging", "vacuum", "valley", "valued", "values", "valves", "vanity", "vapour", "varied", "varies", "vastly", "velvet",
                    "vendor", "venues", "verses", "versus", "vessel", "victim", "videos", "viewed", "viewer", "vigour", "villas", "violin", "virtue", "vision", "visits", "voices", "volume", "voters",
                    "voting", "vowels", "voyage", "wagons", "waited", "waiter", "waking", "walked", "wallet", "walnut", "wander", "wanted", "warden", "warily", "warmer", "warmly", "warmth", "warned", "washed",
                    "wasted", "wastes", "waters", "waving", "weaken", "weaker", "weakly", "wealth", "weapon", "weekly", "weight", "whales", "wheels", "whilst", "whisky", "whites", "wholly", "wicket", "widely",
                    "widest", "widows", "wildly", "window", "winger", "winner", "winter", "wiping", "wiring", "wisdom", "wisely", "wished", "wishes", "within", "wizard", "wolves", "wonder", "worked", "worker",
                    "worlds", "wounds", "wrists", "writer", "writes", "yachts", "yelled", "yields", "youths"
                }
            }
        };
    }
}