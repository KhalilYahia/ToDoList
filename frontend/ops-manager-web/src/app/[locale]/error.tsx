"use client";

import { useEffect } from "react";

import { Alert, Button } from "@/components/ui/primitives";

export default function ErrorPage({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="mx-auto grid min-h-screen max-w-xl place-content-center gap-4 p-6">
      <Alert title="This page could not be loaded" tone="danger">
        Try again. If the problem continues, contact your administrator.
      </Alert>
      <Button onClick={reset}>Try again</Button>
    </main>
  );
}
