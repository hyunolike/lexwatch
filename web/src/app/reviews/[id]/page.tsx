import { Suspense } from "react";
import { ReviewClient } from "./ReviewClient";

async function Loaded({ params }: { params: PageProps<"/reviews/[id]">["params"] }) {
  const { id } = await params;
  return <ReviewClient id={id} />;
}

export default function ReviewPage({ params }: PageProps<"/reviews/[id]">) {
  return (
    <Suspense fallback={<p className="p-8 text-center text-ui text-ink-subtle">불러오는 중이에요</p>}>
      <Loaded params={params} />
    </Suspense>
  );
}
