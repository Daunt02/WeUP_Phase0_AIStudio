/**
 * M6-P29 TEMPORAL NAVIGATION v1.0
 *
 * COMPLETE FILE INDEX & NAVIGATION GUIDE
 *
 * Use this file to find what you need quickly
 */

// ============================================================================
// QUICK NAVIGATION BY TASK
// ============================================================================

const taskToFile = {
  "I want to: Get started quickly": {
    file: "README_TEMPORAL_NAVIGATION.md",
    section: "Quick Start (5 steps)",
    also_read: "QUICK_REFERENCE.ts (one-page overview)",
  },

  "I want to: Understand the complete system": {
    file: "README_TEMPORAL_NAVIGATION.md",
    section: "Full README (architecture, concepts, API)",
    also_read: "TEMPORAL_NAVIGATION_GUIDE.ts (parts 1-5)",
  },

  "I want to: See working code examples": {
    file: "TEMPORAL_INTEGRATION_EXAMPLE.ts",
    section: "Minimal integration + template",
    also_read: "useTemporalNavigation.ts (source with comments)",
  },

  "I want to: Copy-paste integration boilerplate": {
    file: "TEMPORAL_INTEGRATION_EXAMPLE.ts",
    section: "Minimal Integration Example + Template snippet",
    time_to_integrate: "5 minutes",
  },

  "I want to: Learn all backend query patterns": {
    file: "TEMPORAL_NAVIGATION_GUIDE.ts",
    section: "PART 2: Backend Query Examples (6 scenarios)",
    includes: [
      "Preset selection",
      "Date stepping",
      "Custom ranges",
      "Scrubbing",
    ],
  },

  "I want to: Replace MapTimeFilterPanel": {
    file: "MIGRATION_GUIDE.ts",
    section: "OPTION A: Full Replacement",
    also_read: "Comparison table & migration checklist",
  },

  "I want to: Keep both old and new": {
    file: "MIGRATION_GUIDE.ts",
    section: "OPTION B: Coexistence During Transition",
    also_read: "Component composition details",
  },

  "I want to: Understand scrubbing semantics": {
    file: "TEMPORAL_NAVIGATION_GUIDE.ts",
    section: "PART 4: Scrubbing Technical Details",
    key_insight: "Scrubbing affects CALENDAR focus, NOT backend window",
  },

  "I want to: Write tests": {
    file: "TEMPORAL_NAVIGATION_GUIDE.ts",
    section: "PART 7: Testing Checklist",
    includes: ["Unit", "Component", "Integration", "E2E"],
  },

  "I want to: Debug temporal state": {
    file: "TEMPORAL_INTEGRATION_EXAMPLE.ts",
    section: "State Debugging Helper",
    also_read: "TemporalNavigationControls.vue (debug panel in dev mode)",
  },

  "I want to: Handle validation errors": {
    file: "TEMPORAL_NAVIGATION_GUIDE.ts",
    section: "PART 6: Error Handling & Edge Cases",
    also_read: "TEMPORAL_INTEGRATION_EXAMPLE.ts (user-friendly messages)",
  },

  "I want to: Make it mobile-friendly": {
    file: "README_TEMPORAL_NAVIGATION.md",
    section: "Mobile & Accessibility",
    also_read: "TemporalNavigationControls.vue (media queries)",
  },

  "I want to: Know all API methods": {
    file: "QUICK_REFERENCE.ts",
    section: "API Quick Reference",
    also_read: "useTemporalNavigation.ts (JSDoc comments)",
  },

  "I want to: Understand guardrails": {
    file: "TEMPORAL_NAVIGATION_GUIDE.ts",
    section: "PART 3: State Management & Guardrails",
    key_topics: ["Validation", "Event stability", "Debouncing", "Timezone"],
  },

  "I want to: See all files created": {
    file: "IMPLEMENTATION_SUMMARY.ts",
    section: "Deliverables Checklist",
    also_read: "This INDEX file",
  },

  "I want to: Troubleshoot issues": {
    file: "README_TEMPORAL_NAVIGATION.md",
    section: "Troubleshooting Q&A",
    also_read: "MIGRATION_GUIDE.ts (Common Gotchas)",
  },

  "I want to: Understand the architecture": {
    file: "README_TEMPORAL_NAVIGATION.md",
    section: "Architecture (diagram + descriptions)",
    visual: "Architecture diagram with file responsibilities",
  },
};

// ============================================================================
// COMPLETE FILE STRUCTURE
// ============================================================================

const fileStructure = {
  src: {
    composables: {
      "useTemporalNavigation.ts": {
        lines: 230,
        purpose: "Main composable for temporal navigation",
        exports: [
          "useTemporalNavigation(options) — Main hook",
          "selectPreset(preset) × method",
          "stepDateTime(units, unit) × method",
          "updateScrubPosition(position) × method",
          "resetToNow() × method",
          "buildMapFeedQuery(bbox) × method",
          "toCalendarOverlayTemporalQuery() × method",
          "shouldClearSelectedEventOnWindowShift() × method",
        ],
        when_to_use: "Always — core of the system, initialize at app level",
        dependencies: ["useMapFeedFilters.ts"],
        learn_from: [
          "Comments explain temporal semantics",
          "JSDoc blocks show parameters & returns",
          "Debounce logic shown in debouncedTemporalUpdate()",
        ],
      },

      "TEMPORAL_INTEGRATION_EXAMPLE.ts": {
        lines: 400,
        purpose: "Working code examples and integration patterns",
        includes: [
          "Minimal integration setup",
          "App.vue complete template example",
          "State debugging helper function",
          "Common patterns: reset, URL share, restore, keyboard",
          "User-friendly error message generator",
        ],
        when_to_use: "Copy snippets to your app; reference for patterns",
        learning_level: "Intermediate — shows practical usage",
      },
    },

    components: {
      "TemporalNavigationControls.vue": {
        lines: 500,
        purpose: "Vue 3 + Quasar component for all temporal UI",
        renders: [
          "5 preset buttons (Now, Tonight, Tomorrow, This Weekend, Custom)",
          "Temporal status with validation errors",
          "Date step controls (±1 day arrows)",
          "Interactive timeline scrubber (drag to 0-1)",
          "Reset button",
          "Mobile responsive layout",
          "Dev-mode debug panel",
        ],
        interactions: [
          "Click buttons",
          "Drag scrubber",
          "Mouse & touch support",
        ],
        styling: [
          "Glassmorphism card",
          "WCAG AA color contrast",
          "Responsive grid",
        ],
        when_to_use: "In parent template; pass temporal composable as prop",
        dependencies: ["useTemporalNavigation.ts", "Quasar components"],
        learn_from: [
          "Scrubber event handling (mouse + touch)",
          "Mobile responsive design (media queries)",
          "Accessibility (ARIA roles, labels)",
          "Styled components (Quasar q-btn, q-card, etc)",
        ],
      },

      "README_TEMPORAL_NAVIGATION.md": {
        lines: 800,
        format: "Markdown (readable in GitHub/editor)",
        purpose: "Main reference guide",
        sections: [
          "Overview & capabilities",
          "Architecture diagram & file responsibility",
          "Quick start (5 steps)",
          "Key concepts (presets, stepping, scrubbing)",
          "Backend contract examples",
          "Validation & error handling",
          "Mobile & accessibility",
          "Testing checklist",
          "Troubleshooting Q&A",
          "Future enhancements",
        ],
        when_to_use: "Start here for complete system understanding",
        learning_level: "Beginner-friendly overview",
      },

      "TEMPORAL_NAVIGATION_GUIDE.ts": {
        lines: 700,
        format: "TypeScript with detailed comments",
        purpose: "Comprehensive technical guide",
        sections: [
          "Part 1: Component integration patterns",
          "Part 2: 6 canonical backend query scenarios (with actual JSON)",
          "Part 3: State management & 5 guardrails",
          "Part 4: Scrubbing technical details (position math, event flow)",
          "Part 5: Accessibility & mobile UX best practices",
          "Part 6: Error handling & 5 edge cases",
          "Part 7: Testing checklist (unit, component, integration, e2e)",
          "Part 8: Future enhancement ideas",
        ],
        when_to_use:
          "Reference for specific questions; testing; backend integration",
        learning_level: "Advanced — detailed semantics",
      },

      "MIGRATION_GUIDE.ts": {
        lines: 400,
        format: "TypeScript with code templates",
        purpose: "Guide for transitioning from MapTimeFilterPanel",
        sections: [
          "Option A: Full replacement (with before/after code)",
          "Option B: Coexistence (Advanced filters expansion)",
          "Comparison table (old vs new)",
          "Architecture composition details",
          "Migration checklist",
          "Deprecation path (3-6 month plan)",
          "5 common gotchas with solutions",
        ],
        when_to_use: "If you have existing MapTimeFilterPanel",
        learning_level: "Intermediate",
      },
    },

    "IMPLEMENTATION_SUMMARY.ts": {
      lines: 400,
      format: "TypeScript with structured data objects",
      purpose: "Complete implementation checklist & summary",
      includes: [
        "Deliverables checklist",
        "Requirements mapped to implementation",
        "All 5-step quick start",
        "File locations",
        "Key concepts at a glance",
        "Testing entry points",
        "Integration checklist",
        "Troubleshooting matrix",
        "Performance notes",
      ],
      when_to_use: "Project planning, team communication, progress tracking",
      learning_level: "Summary — facts and figures",
    },

    "QUICK_REFERENCE.ts": {
      lines: 300,
      format: "TypeScript with ASCII diagrams",
      purpose: "One-page desk reference",
      includes: [
        "API quick reference (methods, state, computed)",
        "Integration template (copy-paste)",
        "Visual UI structure (ASCII diagram)",
        "State machine (interactions to API calls)",
        "Backend contract mapping (3 scenarios)",
        "6 common mistakes & fixes",
        "Feature checklist",
        "Where to learn more",
        "Production readiness",
      ],
      when_to_use: "Print or bookmark; daily reference during coding",
      learning_level: "Cheat sheet",
    },
  },
};

// ============================================================================
// FILE DEPENDENCY GRAPH
// ============================================================================

const dependencies = {
  "TemporalNavigationControls.vue": [
    "useTemporalNavigation.ts (via prop :temporal)",
    "Quasar components (q-btn, q-card, q-spinner, q-icon)",
  ],

  "useTemporalNavigation.ts": [
    "useMapFeedFilters.ts (wraps & extends)",
    "time-window.contracts.ts (types)",
  ],

  "useMapFeedFilters.ts": ["time-window.contracts.ts (types)"],

  "TEMPORAL_NAVIGATION_GUIDE.ts": [
    "Documentation only; no runtime dependencies",
  ],

  "TEMPORAL_INTEGRATION_EXAMPLE.ts": [
    "useTemporalNavigation.ts (in example code)",
    "TemporalNavigationControls.vue (in example code)",
  ],
};

// ============================================================================
// READING PATHS (From Beginner to Advanced)
// ============================================================================

const readingPaths = {
  beginner: {
    time: "15 minutes",
    path: [
      "This INDEX file (you are here)",
      "QUICK_REFERENCE.ts (one-page overview)",
      "README_TEMPORAL_NAVIGATION.md (Quick Start section only)",
      "TemporalNavigationControls.vue (visual code, see the UI)",
    ],
    outcome: "Can integrate component into existing app",
  },

  intermediate: {
    time: "45 minutes",
    path: [
      "README_TEMPORAL_NAVIGATION.md (full readme)",
      "TEMPORAL_INTEGRATION_EXAMPLE.ts (copy code patterns)",
      "TEMPORAL_NAVIGATION_GUIDE.ts (Parts 2-3: backend & guardrails)",
      "Try: Implement in a test branch",
    ],
    outcome: "Can customize, handle errors, debug issues",
  },

  advanced: {
    time: "2 hours",
    path: [
      "useTemporalNavigation.ts (read source code comments)",
      "TemporalNavigationControls.vue (read implementation)",
      "TEMPORAL_NAVIGATION_GUIDE.ts (all 8 parts)",
      "MIGRATION_GUIDE.ts (all options & gotchas)",
      "IMPLEMENTATION_SUMMARY.ts (testing & performance sections)",
    ],
    outcome: "Can extend system, write tests, optimize performance",
  },

  expert: {
    time: "4 hours",
    path: [
      "Read all files in this entire implementation",
      "Study useMapFeedFilters.ts (foundation)",
      "Write unit & component tests",
      "Create integration tests with real backend calls",
      "Performance profile in browser DevTools",
      "Consider post-MVP enhancements",
    ],
    outcome: "Can maintain, enhance, document, and support others",
  },
};

// ============================================================================
// SEARCH INDEX (Find by keyword)
// ============================================================================

const searchIndex = {
  scrubbing: [
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 4",
    "README_TEMPORAL_NAVIGATION.md Key Concepts",
    "useTemporalNavigation.ts updateScrubPosition() method",
    "QUICK_REFERENCE.ts Scrubbing explanation",
  ],

  validation: [
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 3 Guardrail 1",
    "TEMPORAL_INTEGRATION_EXAMPLE.ts error message helper",
    "README_TEMPORAL_NAVIGATION.md Validation & Error Handling",
  ],

  backend_query: [
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 2 (6 examples)",
    "QUICK_REFERENCE.ts Backend Contract Mapping",
    "useTemporalNavigation.ts buildMapFeedQuery() method",
  ],

  mobile: [
    "TemporalNavigationControls.vue (media queries in styles)",
    "README_TEMPORAL_NAVIGATION.md Mobile & Accessibility section",
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 5",
  ],

  accessibility: [
    "TemporalNavigationControls.vue (ARIA attributes)",
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 5",
    "README_TEMPORAL_NAVIGATION.md Accessibility subsection",
  ],

  debouncing: [
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 3 Guardrail 3",
    "useTemporalNavigation.ts debouncedTemporalUpdate() function",
    "TEMPORAL_INTEGRATION_EXAMPLE.ts watch pattern",
  ],

  event_selection: [
    "useTemporalNavigation.ts shouldClearSelectedEventOnWindowShift()",
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 3 Guardrail 2",
    "TEMPORAL_INTEGRATION_EXAMPLE.ts event selection watch example",
  ],

  testing: [
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 7 (full checklist)",
    "IMPLEMENTATION_SUMMARY.ts Testing Entry Points",
  ],

  migration: [
    "MIGRATION_GUIDE.ts (complete migration guide)",
    "README_TEMPORAL_NAVIGATION.md File Locations showing both",
  ],

  errors_fixing: [
    "MIGRATION_GUIDE.ts Common Gotchas",
    "README_TEMPORAL_NAVIGATION.md Troubleshooting",
    "TEMPORAL_INTEGRATION_EXAMPLE.ts error helpers",
  ],

  performance: [
    "IMPLEMENTATION_SUMMARY.ts Performance Notes",
    "TEMPORAL_NAVIGATION_GUIDE.ts Part 7 edge cases",
  ],
};

// ============================================================================
// CHECKLIST: Before You Start
// ============================================================================

const preflightChecklist = [
  "☐ Vue 3 project with Composition API",
  "☐ Quasar Framework installed & configured",
  "☐ TypeScript enabled",
  "☐ Existing useMapFeedFilters.ts available",
  "☐ Existing time-window.contracts.ts available",
  "☐ Can access frontend-vue/src/composables/ directory",
  "☐ Can access frontend-vue/src/components/ directory",
  "☐ Have MapSurface and CalendarOverlay components ready",
];

// ============================================================================
// CHECKLIST: After Implementation
// ============================================================================

const postImplementationChecklist = [
  "☐ useTemporalNavigation.ts copied to src/composables/",
  "☐ TemporalNavigationControls.vue copied to src/components/",
  "☐ All documentation files copied to respective directories",
  "☐ No TypeScript errors (npm run build)",
  "☐ Component renders without errors in browser",
  "☐ Preset buttons respond to clicks",
  "☐ Step buttons increment/decrement date display",
  "☐ Scrubber drag is smooth",
  "☐ Reset button clears state",
  "☐ Validation errors display when entering invalid range",
  "☐ Map query updates automatically on temporal change",
  "☐ Calendar query stays in sync with temporal state",
  "☐ Works on mobile (< 512px width)",
  "☐ Touch drag works on mobile/tablet",
  "☐ Selected event handling implemented",
  "☐ Team notified of changes",
  "☐ Documentation shared with team",
];

// ============================================================================
// HELP & SUPPORT FLOW
// ============================================================================

const supportFlow = `
PROBLEM OCCURS
     │
     ▼
Is it about architecture/how it works?
├─ Yes → README_TEMPORAL_NAVIGATION.md
├─ No → Next question
     │
     ▼
Is it about backend query format?
├─ Yes → TEMPORAL_NAVIGATION_GUIDE.ts Part 2
├─ No → Next question
     │
     ▼
Is it about why state changed unexpectedly?
├─ Yes → TEMPORAL_NAVIGATION_GUIDE.ts Part 3 (guardrails)
├─ No → Next question
     │
     ▼
Is it about scrubbing?
├─ Yes → TEMPORAL_NAVIGATION_GUIDE.ts Part 4
├─ No → Next question
     │
     ▼
Is it a mobile/accessibility issue?
├─ Yes → TEMPORAL_NAVIGATION_GUIDE.ts Part 5
├─ No → Next question
     │
     ▼
Is it an error or edge case?
├─ Yes → TEMPORAL_NAVIGATION_GUIDE.ts Part 6
├─ No → Next question
     │
     ▼
Am I replacing MapTimeFilterPanel?
├─ Yes → MIGRATION_GUIDE.ts + common gotchas
├─ No → Next question
     │
     ▼
Do I need working code examples?
├─ Yes → TEMPORAL_INTEGRATION_EXAMPLE.ts
├─ No → Next question
     │
     ▼
Still stuck?
└─ Enable debug panel (import.meta.env.DEV)
  or check browser console for specific errors
`;

// ============================================================================
// EXPORT
// ============================================================================

export {
  taskToFile,
  fileStructure,
  dependencies,
  readingPaths,
  searchIndex,
  preflightChecklist,
  postImplementationChecklist,
  supportFlow,
};

console.log(`
╔═══════════════════════════════════════════════════════════════╗
║  M6-P29 TEMPORAL NAVIGATION v1.0 - FILE INDEX                ║
║                                                               ║
║  Use taskToFile to find what you need                         ║
║  Use readingPaths to plan your learning                       ║
║  Use searchIndex to find topics by keyword                    ║
║  Use supportFlow for troubleshooting                          ║
╚═══════════════════════════════════════════════════════════════╝
`);
