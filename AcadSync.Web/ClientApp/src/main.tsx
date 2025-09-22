import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Routes, Route, Link } from "react-router-dom";
import RulesList from "./pages/RulesList";
import RuleEditor from "./pages/RuleEditor";

function App() {
  return (
    <BrowserRouter>
      <div style={{ padding: 16 }}>
        <h1>AcadSync Admin</h1>
        <nav>
          <Link to="/">Rules</Link> | <Link to="/editor">Editor</Link>
        </nav>
        <Routes>
          <Route path="/" element={<RulesList />} />
          <Route path="/editor/:id?" element={<RuleEditor />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

createRoot(document.getElementById("root")!).render(<App />);
