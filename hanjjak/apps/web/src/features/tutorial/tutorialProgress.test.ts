import { expect, it } from "vitest";
import {
  ADVENTURER_TUTORIAL_ID,
  readTutorialProgress,
  tutorialProgressKey,
  writeTutorialProgress,
} from "./tutorialProgress";

function memoryStorage(): Pick<Storage, "getItem" | "setItem"> {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
}

it("keeps tutorial progress isolated per account", () => {
  const storage = memoryStorage();
  writeTutorialProgress(storage, "account-a", ADVENTURER_TUTORIAL_ID, "COMPLETED");

  expect(readTutorialProgress(storage, "account-a", ADVENTURER_TUTORIAL_ID)).toBe("COMPLETED");
  expect(readTutorialProgress(storage, "account-b", ADVENTURER_TUTORIAL_ID)).toBeNull();
});

it("ignores malformed or unknown progress data", () => {
  const values = new Map<string, string>([
    [tutorialProgressKey("account-a", ADVENTURER_TUTORIAL_ID), "not-json"],
    [tutorialProgressKey("account-b", ADVENTURER_TUTORIAL_ID), JSON.stringify({ status: "ACTIVE" })],
  ]);
  const storage = { getItem: (key: string) => values.get(key) ?? null };

  expect(readTutorialProgress(storage, "account-a", ADVENTURER_TUTORIAL_ID)).toBeNull();
  expect(readTutorialProgress(storage, "account-b", ADVENTURER_TUTORIAL_ID)).toBeNull();
});

it("does not block dismissal when browser storage is unavailable", () => {
  const storage = { setItem: () => { throw new DOMException("blocked"); } };

  expect(() => writeTutorialProgress(storage, "account-a", ADVENTURER_TUTORIAL_ID, "DISMISSED")).not.toThrow();
});
