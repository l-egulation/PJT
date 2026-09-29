export const RUNTIME_DB_NAME = "hanjjak-runtime";
export const RUNTIME_SCHEMA_VERSION = 1;

export type IdleMode = "AUTO_PROGRESS" | "REPEAT_STAGE";
export type RuntimeCheckpoint = {
  accountId: string;
  schemaVersion: number;
  contentVersion: string;
  idleMode: IdleMode;
  stageId: string;
  hp: number;
  defeatedNormals: number;
  savedAt: string;
};

export interface RuntimeCheckpointStore {
  load(accountId: string): Promise<RuntimeCheckpoint | null>;
  save(checkpoint: RuntimeCheckpoint): Promise<void>;
  clearRuntime(accountId: string): Promise<void>;
}

const CHECKPOINTS = "checkpoints";

export class IndexedDbRuntimeCheckpointStore implements RuntimeCheckpointStore {
  constructor(private readonly indexedDb: IDBFactory = globalThis.indexedDB) {}

  async load(accountId: string): Promise<RuntimeCheckpoint | null> {
    const checkpoint = await this.read(accountId);
    if (checkpoint?.schemaVersion === RUNTIME_SCHEMA_VERSION) return checkpoint;
    if (checkpoint) await this.clearRuntime(accountId);
    return null;
  }

  async save(checkpoint: RuntimeCheckpoint): Promise<void> {
    const db = await this.openWithRecovery();
    const transaction = db.transaction(CHECKPOINTS, "readwrite");
    transaction.objectStore(CHECKPOINTS).put(checkpoint, checkpoint.accountId);
    await transactionDone(transaction);
    db.close();
  }

  async clearRuntime(accountId: string): Promise<void> {
    const db = await this.openWithRecovery();
    const transaction = db.transaction(CHECKPOINTS, "readwrite");
    transaction.objectStore(CHECKPOINTS).delete(accountId);
    await transactionDone(transaction);
    db.close();
  }

  private async read(accountId: string): Promise<RuntimeCheckpoint | null> {
    const db = await this.openWithRecovery();
    const request = db.transaction(CHECKPOINTS).objectStore(CHECKPOINTS).get(accountId);
    const result = await requestResult<RuntimeCheckpoint | undefined>(request);
    db.close();
    return result ?? null;
  }

  private async openWithRecovery(): Promise<IDBDatabase> {
    try {
      return await openDatabase(this.indexedDb);
    } catch {
      await deleteDatabase(this.indexedDb);
      return openDatabase(this.indexedDb);
    }
  }
}

function openDatabase(indexedDb: IDBFactory): Promise<IDBDatabase> {
  const { promise, resolve, reject } = Promise.withResolvers<IDBDatabase>();
  const request = indexedDb.open(RUNTIME_DB_NAME, RUNTIME_SCHEMA_VERSION);
  request.onupgradeneeded = () => {
    const db = request.result;
    if (!db.objectStoreNames.contains(CHECKPOINTS)) db.createObjectStore(CHECKPOINTS);
  };
  request.onsuccess = () => resolve(request.result);
  request.onerror = () => reject(request.error);
  return promise;
}

function deleteDatabase(indexedDb: IDBFactory): Promise<void> {
  const { promise, resolve, reject } = Promise.withResolvers<void>();
  const request = indexedDb.deleteDatabase(RUNTIME_DB_NAME);
  request.onsuccess = () => resolve();
  request.onerror = () => reject(request.error);
  request.onblocked = () => reject(new Error("IndexedDB reset blocked"));
  return promise;
}

function requestResult<T>(request: IDBRequest<T>): Promise<T> {
  const { promise, resolve, reject } = Promise.withResolvers<T>();
  request.onsuccess = () => resolve(request.result);
  request.onerror = () => reject(request.error);
  return promise;
}

function transactionDone(transaction: IDBTransaction): Promise<void> {
  const { promise, resolve, reject } = Promise.withResolvers<void>();
  transaction.oncomplete = () => resolve();
  transaction.onerror = () => reject(transaction.error);
  transaction.onabort = () => reject(transaction.error);
  return promise;
}
