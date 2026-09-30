/**
 * WEUP-2.5D (D17): interaction state machine — deterministic unit tests.
 *
 * Covers the pure helpers (breakpoint boundaries, single-focus rule,
 * corridor emphasis priority, inspector/command/signal transitions) and the
 * useInteractionStateMachine coordinator (composition from viewport width,
 * selection-over-focus enforcement, emphasized-district derivation,
 * background dimming). Pure logic: no DOM, no map, no backend. All helpers
 * are deterministic — same input always yields the same state (mission §XIV).
 */
import { ref } from "vue";
import { describe, expect, it } from "vitest";
import {
  COMPOSITION_BREAKPOINTS,
  clusterStateFor,
  commandStateFor,
  compositionForWidth,
  corridorStateFor,
  depthPlaneFor,
  inspectorStateFor,
  signalStateFor,
  useInteractionStateMachine,
  type InteractionInputs,
} from "../composables/useInteractionStateMachine";
import type { WeupNavMode } from "../navigation/weupNavModes";

describe("compositionForWidth", () => {
  it("maps the canonical breakpoints exactly (mobile|tablet|desktop|wide)", () => {
    expect(compositionForWidth(0)).toBe("mobile");
    expect(compositionForWidth(767)).toBe("mobile");
    expect(compositionForWidth(768)).toBe("tablet");
    expect(compositionForWidth(1023)).toBe("tablet");
    expect(compositionForWidth(1024)).toBe("desktop");
    expect(compositionForWidth(1439)).toBe("desktop");
    expect(compositionForWidth(1440)).toBe("wide");
    expect(compositionForWidth(2560)).toBe("wide");
  });

  it("is deterministic at boundaries and clamps negative widths to mobile", () => {
    expect(compositionForWidth(768)).toBe(compositionForWidth(768));
    expect(compositionForWidth(-100)).toBe("mobile");
  });

  it("exports the canonical breakpoint constants", () => {
    expect(COMPOSITION_BREAKPOINTS).toEqual({
      mobileMax: 767,
      tabletMin: 768,
      tabletMax: 1023,
      desktopMin: 1024,
      desktopMax: 1439,
      wideMin: 1440,
    });
  });
});

describe("clusterStateFor (one FOCUSED, one SELECTED)", () => {
  it("SELECTED wins over FOCUSED for the same cluster", () => {
    expect(
      clusterStateFor("c1", {
        focusedClusterId: "c1",
        selectedEventId: "c1",
      }),
    ).toBe("selected");
  });

  it("FOCUSED applies only when nothing is selected", () => {
    expect(
      clusterStateFor("c1", {
        focusedClusterId: "c1",
        selectedEventId: null,
      }),
    ).toBe("focused");
    // A selection anywhere clears focus everywhere (single-focus rule).
    expect(
      clusterStateFor("c1", {
        focusedClusterId: "c1",
        selectedEventId: "c2",
      }),
    ).toBe("visible");
  });

  it("is visible when neither focused nor selected", () => {
    expect(
      clusterStateFor("c1", {
        focusedClusterId: null,
        selectedEventId: null,
      }),
    ).toBe("visible");
  });
});

describe("corridorStateFor (emphasized > active > visible)", () => {
  const edge = { a: "downtown", b: "midtown" };

  it("emphasizes edges touching the selected cluster district", () => {
    expect(
      corridorStateFor(edge, {
        emphasizedDistrict: "downtown",
        activeDistrict: "downtown",
      }),
    ).toBe("emphasized");
  });

  it("marks edges touching only the filter district active", () => {
    expect(
      corridorStateFor(edge, {
        emphasizedDistrict: null,
        activeDistrict: "midtown",
      }),
    ).toBe("active");
  });

  it("leaves idle edges visible", () => {
    expect(
      corridorStateFor(edge, {
        emphasizedDistrict: null,
        activeDistrict: "uptown",
      }),
    ).toBe("visible");
    expect(corridorStateFor(edge, {})).toBe("visible");
  });
});

describe("inspectorStateFor", () => {
  it("is closed when the inspector is not open", () => {
    expect(inspectorStateFor({ open: false, expanded: false })).toBe("closed");
  });

  it("is visible while loading, expanded when loaded", () => {
    expect(inspectorStateFor({ open: true, expanded: false })).toBe("visible");
    expect(inspectorStateFor({ open: true, expanded: true })).toBe("expanded");
  });
});

describe("commandStateFor", () => {
  const idle = {
    assistantOpen: false,
    wizardOpen: false,
    wizardDegraded: false,
    thinking: false,
  };

  it("is actionable while the assistant thinks or the wizard is open", () => {
    expect(
      commandStateFor({ ...idle, assistantOpen: true, thinking: true }),
    ).toBe("actionable");
    expect(commandStateFor({ ...idle, wizardOpen: true })).toBe("actionable");
  });

  it("is degraded when the wizard errors — even while thinking", () => {
    expect(
      commandStateFor({
        ...idle,
        assistantOpen: true,
        thinking: true,
        wizardOpen: true,
        wizardDegraded: true,
      }),
    ).toBe("degraded");
  });

  it("is visible when the assistant is open but idle, idle otherwise", () => {
    expect(commandStateFor({ ...idle, assistantOpen: true })).toBe("visible");
    expect(commandStateFor(idle)).toBe("idle");
  });
});

describe("signalStateFor", () => {
  const idle = {
    selected: false,
    expanded: false,
    actionable: false,
    archived: false,
  };

  it("applies priority archived > actionable > expanded > selected > idle", () => {
    expect(signalStateFor({ ...idle, archived: true, selected: true })).toBe(
      "archived",
    );
    expect(signalStateFor({ ...idle, actionable: true, expanded: true })).toBe(
      "actionable",
    );
    expect(signalStateFor({ ...idle, expanded: true, selected: true })).toBe(
      "expanded",
    );
    expect(signalStateFor({ ...idle, selected: true })).toBe("selected");
    expect(signalStateFor(idle)).toBe("idle");
  });
});

describe("depthPlaneFor", () => {
  it("maps inspector → z3 and command → z4", () => {
    expect(depthPlaneFor("inspector")).toBe("z3");
    expect(depthPlaneFor("command")).toBe("z4");
  });
});

describe("useInteractionStateMachine", () => {
  function makeInputs(): InteractionInputs {
    return {
      viewportWidth: ref(1100),
      temporalDayKey: ref<string | null>(null),
      selectedEventId: ref<string | null>(null),
      selectedDistrict: ref<string | null>(null),
      activeDistrict: ref<string | null | undefined>(null),
      inspectorOpen: ref(false),
      inspectorExpanded: ref(false),
      assistantOpen: ref(false),
      wizardOpen: ref(false),
      wizardDegraded: ref(false),
      assistantThinking: ref(false),
      activeNavMode: ref<WeupNavMode>("DISCOVER"),
      sideInspectorOpen: ref(false),
    };
  }

  it("derives composition from the viewport width ref", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    expect(machine.composition.value).toBe("desktop");
    inputs.viewportWidth.value = 500;
    expect(machine.composition.value).toBe("mobile");
    inputs.viewportWidth.value = 800;
    expect(machine.composition.value).toBe("tablet");
    inputs.viewportWidth.value = 1500;
    expect(machine.composition.value).toBe("wide");
  });

  it("enforces single focus: selection clears focus, temporal key focuses", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    expect(machine.focusedClusterId.value).toBeNull();
    inputs.temporalDayKey.value = "2026-09-30";
    expect(machine.focusedClusterId.value).toBe("2026-09-30");
    expect(machine.clusterStateForId("2026-09-30")).toBe("focused");
    // Selection wins: focus is cleared (one SELECTED, zero FOCUSED ambiguity).
    inputs.selectedEventId.value = "evt-1";
    expect(machine.focusedClusterId.value).toBeNull();
    expect(machine.clusterStateForId("evt-1")).toBe("selected");
    expect(machine.clusterStateForId("2026-09-30")).toBe("visible");
  });

  it("supports explicit focus requests, ignored while selected", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    machine.requestFocus("c9");
    expect(machine.focusedClusterId.value).toBe("c9");
    machine.clearFocus();
    expect(machine.focusedClusterId.value).toBeNull();
    // Explicit focus never overrides an active selection.
    inputs.selectedEventId.value = "evt-1";
    machine.requestFocus("c9");
    expect(machine.focusedClusterId.value).toBeNull();
  });

  it("derives the emphasized corridor district from the selected district", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    const edge = { a: "downtown", b: "midtown" };
    expect(machine.emphasizedCorridorDistrict.value).toBeNull();
    expect(machine.corridorStateForEdge(edge)).toBe("visible");
    inputs.activeDistrict.value = "midtown";
    expect(machine.corridorStateForEdge(edge)).toBe("active");
    // Selection emphasis outranks the filter district.
    inputs.selectedDistrict.value = "downtown";
    expect(machine.emphasizedCorridorDistrict.value).toBe("downtown");
    expect(machine.corridorStateForEdge(edge)).toBe("emphasized");
  });

  it("dims the background when an inspector opens or a command is actionable", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    expect(machine.dimBackground.value).toBe(false);
    inputs.inspectorOpen.value = true;
    expect(machine.inspector.value).toBe("visible");
    expect(machine.dimBackground.value).toBe(true);
    inputs.inspectorOpen.value = false;
    inputs.assistantOpen.value = true;
    inputs.assistantThinking.value = true;
    expect(machine.command.value).toBe("actionable");
    expect(machine.dimBackground.value).toBe(true);
  });

  it("passes navigation mode and the side-inspector flag through", () => {
    const inputs = makeInputs();
    const machine = useInteractionStateMachine(inputs);
    expect(machine.navigation.value).toBe("DISCOVER");
    expect(machine.sideInspectorOpen.value).toBe(false);
    inputs.activeNavMode.value = "SAVED";
    inputs.sideInspectorOpen.value = true;
    expect(machine.navigation.value).toBe("SAVED");
    expect(machine.sideInspectorOpen.value).toBe(true);
  });
});
