import React, { useEffect, useState } from "react";
import axios from "axios";
import { Link } from "react-router-dom";

export default function RulesList() {
  const [rules, setRules] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadRules();
  }, []);

  async function loadRules() {
    setLoading(true);
    setError(null);
      try {
      const res = await axios.get("/api/rules");
      if (Array.isArray(res.data)) {
        setRules(res.data);
      } else {
        // Backend returned unexpected shape — protect against runtime errors and log for debugging
        console.warn("Expected rules array from /api/rules but received:", res.data);
        setRules([]);
      }
      } catch (ex: any) {
      setError(ex?.response?.data?.message || ex.message || "Failed to load rules");
    } finally {
      setLoading(false);
    }
  }

  async function deleteRule(id: string) {
    if (!confirm("Delete this rule?")) return;
    try {
      await axios.delete(`/api/rules/${id}`);
      await loadRules();
    } catch (ex: any) {
      alert("Failed to delete rule: " + (ex?.response?.data?.message || ex.message));
    }
  }

  return (
    <div>
      <h2>Rules</h2>
      <div style={{ marginBottom: 12 }}>
        <Link to="/editor">Create new rule</Link>
      </div>

      {loading && <div>Loading...</div>}
      {error && <div style={{ color: "red" }}>{error}</div>}

      {!loading && !error && (
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr>
              <th style={{ textAlign: "left", padding: 8 }}>Name</th>
              <th style={{ textAlign: "left", padding: 8 }}>Slug</th>
              <th style={{ textAlign: "left", padding: 8 }}>Active</th>
              <th style={{ padding: 8 }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {rules.length === 0 && (
              <tr>
                <td colSpan={4} style={{ padding: 8 }}>
                  No rules found.
                </td>
              </tr>
            )}
            {rules.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 8 }}>{r.name}</td>
                <td style={{ padding: 8 }}>{r.slug}</td>
                <td style={{ padding: 8 }}>{r.isActive ? "Yes" : "No"}</td>
                <td style={{ padding: 8 }}>
                  <Link to={`/editor/${r.id}`}>Edit</Link>
                  {" | "}
                  <button onClick={() => deleteRule(r.id)} style={{ marginLeft: 8 }}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
