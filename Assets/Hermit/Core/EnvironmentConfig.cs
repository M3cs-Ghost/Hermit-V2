using UnityEngine;

namespace Hermit.Core
{
    /// <summary>
    /// Data-only environment configuration.
    ///
    /// The Supabase anon key is deliberately stored here in plain text and
    /// versioned in Git like any other data asset — it is not a secret, it is
    /// protected by Postgres Row Level Security, not by concealment (the same
    /// key already ships in V1's browser client today).
    ///
    /// A service_role key must NEVER be placed in this asset, or anywhere in
    /// this repository — it bypasses RLS entirely and only belongs server-side.
    ///
    /// No instance of this asset exists yet. Creating the Development/Staging/
    /// Production .asset files is a manual Unity Editor step for C4, not done
    /// here (see Docs/C4_SUPABASE_SPIKE.md).
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "Hermit/Environment Config")]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        [SerializeField] private HermitEnvironment _environment = HermitEnvironment.Development;
        [SerializeField] private string _supabaseUrl = string.Empty;
        [SerializeField] private string _supabaseAnonKey = string.Empty;

        public HermitEnvironment Environment => _environment;
        public string SupabaseUrl => _supabaseUrl;
        public string SupabaseAnonKey => _supabaseAnonKey;
    }
}
