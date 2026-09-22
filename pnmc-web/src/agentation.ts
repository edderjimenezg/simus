import { Agentation, type AgentationProps } from 'agentation';
import { createElement } from 'react';
import type { ComponentType } from 'react';
import { createRoot } from 'react-dom/client';

const AGENTATION_HOST_ID = 'agentation-root';
const AGENTATION_ENDPOINT = 'http://localhost:4747';

export function mountAgentation(): void {
  if (document.getElementById(AGENTATION_HOST_ID)) {
    return;
  }

  const host = document.createElement('div');
  host.id = AGENTATION_HOST_ID;
  document.body.appendChild(host);

  createRoot(host).render(
    createElement(Agentation as ComponentType<AgentationProps>, {
      endpoint: AGENTATION_ENDPOINT,
      className: 'pnmc-agentation-toolbar',
    }),
  );
}
