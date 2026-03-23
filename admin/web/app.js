const summaryCards = document.getElementById('summaryCards');
const machinesTable = document.getElementById('machinesTable');
const notificationsList = document.getElementById('notificationsList');
const refreshButton = document.getElementById('refreshButton');
const commandForm = document.getElementById('commandForm');
const commandSelect = document.getElementById('commandSelect');
const commandResult = document.getElementById('commandResult');

async function fetchDashboard() {
  const response = await fetch('/api/dashboard');
  const data = await response.json();
  renderSummary(data.summary);
  renderMachines(data.machines);
  renderNotifications(data.notifications);
  renderCommands(data.quick_commands);
}

function renderSummary(summary) {
  const labels = {
    machines_active: 'Máquinas ativas',
    machines_blocked: 'Máquinas bloqueadas',
    pending_notes_total: 'Anotações pendentes',
    promised_payments_total: 'Promessas',
    total_pc_minutes: 'Minutos em PCs',
    total_playstation_minutes: 'Minutos em PlayStation',
  };
  summaryCards.innerHTML = Object.entries(summary)
    .map(([key, value]) => `
      <article class="card">
        <p class="eyebrow">${labels[key] ?? key}</p>
        <strong>${value}</strong>
      </article>
    `)
    .join('');
}

function renderMachines(machines) {
  machinesTable.innerHTML = `
    <table class="table">
      <thead>
        <tr>
          <th>Máquina</th>
          <th>IP</th>
          <th>Status</th>
          <th>Usuário</th>
          <th>Tempo restante</th>
        </tr>
      </thead>
      <tbody>
        ${machines.map(machine => `
          <tr>
            <td>${machine.name}</td>
            <td>${machine.ip}</td>
            <td class="status-${machine.status}">${machine.status}</td>
            <td>${machine.user}</td>
            <td>${machine.remaining_minutes} min</td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;
}

function renderNotifications(notifications) {
  notificationsList.innerHTML = notifications
    .map(item => `<li><strong>${item.title}</strong><p>${item.message}</p></li>`)
    .join('');
}

function renderCommands(commands) {
  commandSelect.innerHTML = commands
    .map(command => `<option value="${command}">${command}</option>`)
    .join('');
}

refreshButton.addEventListener('click', fetchDashboard);
commandForm.addEventListener('submit', async event => {
  event.preventDefault();
  const formData = new FormData(commandForm);
  const payload = Object.fromEntries(formData.entries());
  const response = await fetch('/api/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  const data = await response.json();
  commandResult.textContent = `Comando ${data.command} enviado para ${data.machine}.`;
});

fetchDashboard();
