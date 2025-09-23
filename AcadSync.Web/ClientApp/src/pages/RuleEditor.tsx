import React from "react";
import { useParams, Link } from "react-router-dom";

export default function RuleEditor(): JSX.Element {
  const { id } = useParams<{ id?: string }>();

  return (
    <div style={{ padding: 16 }}>
      <h2>Rule Editor</h2>
      {id ? <p>Editing rule with id: {id}</p> : <p>Create a new rule</p>}
      <p>This is a placeholder editor component. Replace with the real editor UI as needed.</p>
      <p>
        <Link to="/">Back to rules</Link>
      </p>
    </div>
  );
}
