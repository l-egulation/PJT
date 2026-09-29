type DocumentPictureInPictureOptions = {
  width?: number;
  height?: number;
  disallowReturnToOpener?: boolean;
  preferInitialWindowPlacement?: boolean;
};

type DocumentPictureInPictureApi = {
  readonly window: Window | null;
  requestWindow: (options?: DocumentPictureInPictureOptions) => Promise<Window>;
};

type WindowWithDocumentPictureInPicture = Window & {
  readonly documentPictureInPicture?: DocumentPictureInPictureApi;
};

export type PictureInPictureAvailability =
  | { supported: true }
  | { supported: false; reason: "insecure" | "unsupported" };

export type PictureInPictureDocument = {
  window: Window;
  mountNode: HTMLElement;
};

export function getPictureInPictureAvailability(sourceWindow: Window = window): PictureInPictureAvailability {
  if (!sourceWindow.isSecureContext) return { supported: false, reason: "insecure" };
  if (!(sourceWindow as WindowWithDocumentPictureInPicture).documentPictureInPicture) {
    return { supported: false, reason: "unsupported" };
  }
  return { supported: true };
}

export async function openPictureInPictureDocument(
  sourceWindow: Window = window,
  size: { width: number; height: number } = { width: 360, height: 480 },
): Promise<PictureInPictureDocument> {
  const availability = getPictureInPictureAvailability(sourceWindow);
  if (!availability.supported) throw new Error(`DOCUMENT_PICTURE_IN_PICTURE_${availability.reason.toUpperCase()}`);

  const api = (sourceWindow as WindowWithDocumentPictureInPicture).documentPictureInPicture!;
  const pipWindow = await api.requestWindow({
    width: size.width,
    height: size.height,
    disallowReturnToOpener: false,
    preferInitialWindowPlacement: false,
  });

  prepareDocument(sourceWindow.document, pipWindow.document);
  const mountNode = pipWindow.document.createElement("div");
  mountNode.id = "picture-in-picture-root";
  pipWindow.document.body.append(mountNode);
  return { window: pipWindow, mountNode };
}

function prepareDocument(source: Document, target: Document): void {
  target.documentElement.lang = source.documentElement.lang || "ko";
  target.documentElement.className = "picture-in-picture-root-document";
  target.title = `${source.title || "한짝"} — 화면 한켠에서 보기`;
  target.body.className = "picture-in-picture-document";

  source.head.querySelectorAll<HTMLStyleElement | HTMLLinkElement>("style, link[rel=\"stylesheet\"]").forEach((node) => {
    if (node.tagName === "LINK") {
      const sourceLink = node as HTMLLinkElement;
      const link = target.createElement("link");
      link.rel = "stylesheet";
      link.href = sourceLink.href;
      if (sourceLink.media) link.media = sourceLink.media;
      target.head.append(link);
      return;
    }
    target.head.append(node.cloneNode(true));
  });
}
