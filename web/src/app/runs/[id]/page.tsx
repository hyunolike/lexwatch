import { Suspense } from "react";
import { RunClient } from "./RunClient";

async function Loaded({ params }: { params: PageProps<"/runs/[id]">["params"] }) {
  const { id } = await params;
  return <RunClient id={id} />;
}

export default function RunPage({ params }: PageProps<"/runs/[id]">) {
  return (
    <Suspense fallback={<p className="p-8 text-center text-slate-500">불러오는 중이에요</p>}>
      <Loaded params={params} />
    </Suspense>
  );
}
