using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using F;
using LC;

namespace FAToRegex {
    static class Program {
        sealed class _EFA {
            public bool IsAccepting;
            public int Accept;
            public List<KeyValuePair<StringBuilder, _EFA>> Transitions { get; } = new List<KeyValuePair<StringBuilder, _EFA>>();
			public IList<_EFA> FillClosure(IList<_EFA> result = null) {
				if (result == null) result = new List<_EFA>();
				if (result.Contains(this))
					return result;
				result.Add(this);
				foreach (var t in Transitions) {
					t.Value.FillClosure(result);
                }
				
				return result;
            }
			public static IList<KeyValuePair<_EFA, string>> GetIncomingTransitions(IEnumerable<_EFA> closure, _EFA efa, bool includeLoops = true) {
				var result = new List<KeyValuePair<_EFA, string>>();
				foreach (var cfa in closure) {
					foreach (var t in cfa.Transitions) {
						if (includeLoops || t.Value != cfa) {
							if (t.Value == efa) {
								result.Add(new KeyValuePair<_EFA, string>(cfa, t.Key.ToString()));
							}
						}
					}
				}
				return result;
			}
			public IDictionary<_EFA,StringBuilder> FillInputTransitionsGroupedByState(IDictionary<_EFA, StringBuilder> result = null) {
				if(result == null) {
					result = new Dictionary<_EFA, StringBuilder>();
				}
				var h = new HashSet<_EFA>();
				for(var i = 0; i < Transitions.Count;++i) {
					var t = Transitions[i];
					StringBuilder exp;
					if(!result.TryGetValue(t.Value,out exp)) {
						var sb = new StringBuilder(t.Key.ToString());
						result.Add(t.Value, sb);
                    } else {
						if(!h.Add(t.Value)) {
							exp.Remove(exp.Length - 1, 1);
                        } else {
							exp.Insert(0, "(");
                        }
						exp.Append("|");
						exp.Append(t.Key.ToString());
						exp.Append(")");
					}
                }
				return result;
            }
			public int IndexOfTransition(string value) {
				var result = 0;
				foreach(var t in Transitions) {
					if(t.ToString().Equals(value,StringComparison.InvariantCulture)) {
						return result;
                    }
					++result;
                }
				return -1;
            }
		}
        static void Main(string[] args) {
			var exp = "foo|(bar)+|baz";
			Console.WriteLine(exp);
			var fa = FFA.Parse(exp);
            Console.WriteLine(fa);
			Console.WriteLine("IsMatch(\"foo\") = {0}",fa.IsMatch("foo"));
			Console.WriteLine("IsMatch(dfa, \"barbar\") = {0}", FFA.IsMatch(fa.ToDfaTable(),"barbar"));
			var srch = "abcde foo fghij barbar klmnop baz";
			var lc = LexContext.Create(srch);
			Console.WriteLine("Search(\"{0}\")",srch);
			var pos = 0L;
			while (-1<(pos=fa.Search(lc))) {
				Console.WriteLine("\t{0} @ {1}", lc.GetCapture(), pos);
            }
			lc = LexContext.Create(srch);
			Console.WriteLine("Search(dfa, \"{0}\")", srch);
			pos = 0L;
			while (-1 < (pos = FFA.Search(fa.ToDfaTable(), lc))) {
				Console.WriteLine("\t{0} @ {1}", lc.GetCapture(), pos);
			}
		}
		static void DumpEfas(IList<_EFA> efas) {
			var i = 0;
			foreach (var e in efas) {
				Console.WriteLine("{0}q{1}:", e.IsAccepting ? "*" : "", i);
				foreach (var t in e.Transitions) {
					Console.WriteLine("\t{0} -> q{1}", t.Key.ToString(), efas.IndexOf(t.Value));
				}
				++i;
				Console.WriteLine();
			}
		}
        static string EscapeCodepoint(int codepoint) {
            var builder = new StringBuilder();
			switch (codepoint) {
			case '.':
			case '[':
			case ']':
			case '^':
			case '-':
			case '\\':
				builder.Append('\\');
				builder.Append(char.ConvertFromUtf32(codepoint));
				break;
			case '\t':
				builder.Append("\\t");
				break;
			case '\n':
				builder.Append("\\n");
				break;
			case '\r':
				builder.Append("\\r");
				break;
			case '\0':
				builder.Append("\\0");
				break;
			case '\f':
				builder.Append("\\f");
				break;
			case '\v':
				builder.Append("\\v");
				break;
			case '\b':
				builder.Append("\\b");
				break;
			default:
				var s = char.ConvertFromUtf32(codepoint);
				if (!char.IsLetterOrDigit(s, 0) && !char.IsSeparator(s, 0) && !char.IsPunctuation(s, 0) && !char.IsSymbol(s, 0)) {
					if (s.Length == 1) {
						builder.Append("\\u");
						builder.Append(unchecked((ushort)codepoint).ToString("x4"));
					} else {
						builder.Append("\\U");
						builder.Append(codepoint.ToString("x8"));
					}

				} else
					builder.Append(s);
				break;
			}
			return builder.ToString();
        }
		static KeyValuePair<int, int>[] _ToPairs(int[] packedRanges) {
			var result = new KeyValuePair<int, int>[packedRanges.Length / 2];
			for (var i = 0; i < result.Length; ++i) {
				var j = i * 2;
				result[i] = new KeyValuePair<int, int>(packedRanges[j], packedRanges[j + 1]);
			}
			return result;
		}
		static IEnumerable<KeyValuePair<int, int>> _NotRanges(IEnumerable<KeyValuePair<int, int>> ranges) {
			// expects ranges to be normalized
			var last = 0x10ffff;
			using (var e = ranges.GetEnumerator()) {
				if (!e.MoveNext()) {
					yield return new KeyValuePair<int, int>(0x0, 0x10ffff);
					yield break;
				}
				if (e.Current.Key > 0) {
					yield return new KeyValuePair<int, int>(0, unchecked(e.Current.Key - 1));
					last = e.Current.Value;
					if (0x10ffff <= last)
						yield break;
				} else if (e.Current.Key == 0) {
					last = e.Current.Value;
					if (0x10ffff <= last)
						yield break;
				}
				while (e.MoveNext()) {
					if (0x10ffff <= last)
						yield break;
					if (unchecked(last + 1) < e.Current.Key)
						yield return new KeyValuePair<int, int>(unchecked(last + 1), unchecked((e.Current.Key - 1)));
					last = e.Current.Value;
				}
				if (0x10ffff > last)
					yield return new KeyValuePair<int, int>(unchecked((last + 1)), 0x10ffff);

			}
		}
		static int[] _FromPairs(IList<KeyValuePair<int, int>> pairs) {
			var result = new int[pairs.Count * 2];
			for (int ic = pairs.Count, i = 0; i < ic; ++i) {
				var pair = pairs[i];
				var j = i * 2;
				result[j] = pair.Key;
				result[j + 1] = pair.Value;
			}
			return result;
		}
		static void _AppendRangeTo(StringBuilder builder, int[] ranges, int index) {
			var first = ranges[index];
			var last = ranges[index + 1];
			builder.Append(EscapeCodepoint(first));
			if (0 == last.CompareTo(first)) return;
			if (last == first + 1) // spit out 1 length ranges as two chars
			{
				builder.Append(EscapeCodepoint(last));
				return;
			}
			builder.Append('-');
			builder.Append(EscapeCodepoint(last));
		}
		
		static string ToRegex(this FFA fa) {
            var closure = fa.FillClosure();
            IList<_EFA> efas = new List<_EFA>(closure.Count+1);
            var i = 0;
			while(i<=closure.Count) {
				efas.Add(null);
				++i;
            }
			i = 0;
            foreach(var cfa in closure) {
                efas[i] = new _EFA();
                ++i;
            }
            var final = new _EFA();
            final.IsAccepting = true;
            final.Accept = 0;
            efas[i] = final;
            for(i = 0;i < closure.Count; ++i) {
                var e = efas[i];
                var c = closure[i];
                if(c.IsAccepting) {
                    e.Transitions.Add(new KeyValuePair<StringBuilder, _EFA>(new StringBuilder(), final));
                }
				var rngGrps = c.FillInputTransitionRangesGroupedByState();
				foreach (var rngGrp in rngGrps) {
					var tto = efas[closure.IndexOf(rngGrp.Key)];
					var sb = new StringBuilder();
					IList<KeyValuePair<int, int>> rngs = _ToPairs(rngGrp.Value);
					var nrngs = new List<KeyValuePair<int, int>>(_NotRanges(rngs));
					var isNot = false;
					if (nrngs.Count < rngs.Count || (nrngs.Count == rngs.Count && 0x10ffff == rngs[rngs.Count - 1].Value)) {
						isNot = true;
						if (0 != nrngs.Count) {
							sb.Append("^");
						} else {
							sb.Append(".");
						}
						rngs = nrngs;
					}
					var rpairs = _FromPairs(rngs);
					for (var r = 0; r < rpairs.Length; r += 2) {
						_AppendRangeTo(sb, rpairs, r);
					}
					if (isNot || sb.Length != 1 || (char.IsWhiteSpace(sb.ToString(), 0))) {
						sb.Insert(0,'[');
						sb.Append(']');
					}
					e.Transitions.Add(new KeyValuePair<StringBuilder, _EFA>(sb, tto));
				}
            }
			i = 0;
			var done = false;
			while (!done) {
				done = true;
				var innerDone = false;
				while (!innerDone) {
					innerDone = true;
					foreach (var e in efas) {
						if (e.Transitions.Count == 1) {
							var its = _EFA.GetIncomingTransitions(efas, e);
							if (its.Count == 1 && its[0].Key.Transitions.Count == 1) {
								// is a loop?
								if (e.Transitions[0].Value == its[0].Key) {
									if (e.Transitions[0].Key.Length == 1) {
										e.Transitions[0].Key.Append("*");
									} else {
										e.Transitions[0].Key.Insert(0, "(");
										e.Transitions[0].Key.Append(")*");
									}
								} else {
									its[0].Key.Transitions[0] = new KeyValuePair<StringBuilder, _EFA>(its[0].Key.Transitions[0].Key, e.Transitions[0].Value);
									its[0].Key.Transitions[0].Key.Append(e.Transitions[0].Key.ToString());
								}
								innerDone = false;
								efas = efas[0].FillClosure();
							} else {
								foreach (var it in its) {
									// is it a loop?
									if (efas.IndexOf(it.Key) >= efas.IndexOf(e)) {
										// yes
									} else {
										// no
										for (var ii = 0; ii < it.Key.Transitions.Count; ++ii) {
											if (it.Value == it.Key.Transitions[ii].Key.ToString()) {
												it.Key.Transitions[ii] = new KeyValuePair<StringBuilder, _EFA>(it.Key.Transitions[ii].Key, e.Transitions[0].Value);
												it.Key.Transitions[ii].Key.Append(e.Transitions[0].Key.ToString());
												innerDone = false;
												efas = efas[0].FillClosure();
											}
										}
									}
								}
							}
						}
					}
					if (innerDone) {
						efas = efas[0].FillClosure();
					} else
						done = false;
					// combine the unions
					innerDone = false;
					while (!innerDone) {
						innerDone = true;
						foreach (var e in efas) {
							var rgs=e.FillInputTransitionsGroupedByState();
							if(rgs.Count!=e.Transitions.Count) {
								e.Transitions.Clear();
								foreach(var rg in rgs) {
									e.Transitions.Add(new KeyValuePair<StringBuilder, _EFA>(rg.Value, rg.Key));
                                }
								innerDone = false;
								efas = efas[0].FillClosure();
                            }
						}
					}
					if (innerDone) {
						efas = efas[0].FillClosure();
					} else
						done = false;

					// remove the loops
					innerDone = false;
					while (!innerDone) {
						innerDone = true;
						foreach (var e in efas) {
							for(var ii=0;ii< e.Transitions.Count;++ii) {
								var t = e.Transitions[ii];
								if(t.Value==e) {
									// this is a loop
									if (t.Key.Length > 1) {
										t.Key.Insert(0, "(");
										t.Key.Append(")");
									}
									t.Key.Append("*");
									// prepend it to all the other transitions 
									for (var iii = 0; iii < e.Transitions.Count; ++iii) {
										if (ii != iii) {
											var tt = e.Transitions[iii];
											if (tt.Value != e) {
												tt.Key.Insert(0, t.Key.ToString());

											}
										}
									}
									e.Transitions.RemoveAt(ii);
									--ii;
									innerDone = false;
									efas = efas[0].FillClosure();
								}
									
                            }
						}
					}
					if (innerDone) {
						efas = efas[0].FillClosure();
					} else
						done = false;
				}
			}
			var cefa = efas[0].FillClosure();
			DumpEfas(cefa);
			return efas[0].Transitions[0].Key.ToString();
        }
    }
}
