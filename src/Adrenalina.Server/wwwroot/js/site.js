document.addEventListener("submit", event => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement)) return;

  const confirmation = form.dataset.confirm;
  if (confirmation && !window.confirm(confirmation)) {
    event.preventDefault();
    return;
  }

  if (!form.checkValidity()) return;

  form.querySelectorAll("button[type='submit'], input[type='submit']").forEach(button => {
    button.disabled = true;
    button.setAttribute("aria-busy", "true");
  });
});

document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-help-page]");
  if (!(page instanceof HTMLElement)) return;

  const version = page.dataset.helpVersion || "1";
  const storageKey = `adrenalina.admin.help.v${version}`;
  const welcome = page.querySelector("[data-help-welcome]");
  const search = page.querySelector("[data-help-search]");
  const status = page.querySelector("[data-help-search-status]");
  const empty = page.querySelector("[data-help-empty]");
  const articles = [...page.querySelectorAll("[data-help-article]")];
  const checks = [...page.querySelectorAll("[data-help-check]")];
  const progressLabel = page.querySelector("[data-help-progress]");
  const progressBar = page.querySelector("[data-help-progress-bar]");
  const progress = page.querySelector("[role='progressbar']");
  let selectedCategory = "*";

  const readState = () => {
    try {
      return JSON.parse(window.localStorage.getItem(storageKey) || "{}") || {};
    } catch {
      return {};
    }
  };

  const writeState = state => {
    try {
      window.localStorage.setItem(storageKey, JSON.stringify(state));
    } catch {
      // Ajuda e checklist continuam funcionando mesmo sem acesso ao storage.
    }
  };

  const state = readState();
  if (!state.welcomeHandled && welcome) welcome.hidden = false;
  const checked = new Set(Array.isArray(state.checks) ? state.checks : []);
  checks.forEach(check => { check.checked = checked.has(check.value); });

  const updateProgress = () => {
    const completed = checks.filter(check => check.checked).length;
    const total = checks.length;
    if (progressLabel) progressLabel.textContent = `${completed} de ${total} concluídos`;
    if (progressBar) progressBar.style.width = `${total ? Math.round(completed / total * 100) : 0}%`;
    if (progress) progress.setAttribute("aria-valuenow", String(completed));
  };

  const filterArticles = () => {
    const term = (search instanceof HTMLInputElement ? search.value : "").trim().toLocaleLowerCase("pt-BR");
    let visible = 0;
    articles.forEach(article => {
      const categoryMatches = selectedCategory === "*" || article.dataset.helpCategoryValue === selectedCategory;
      const textMatches = !term || (article.dataset.helpSearchText || "").toLocaleLowerCase("pt-BR").includes(term);
      const show = categoryMatches && textMatches;
      article.toggleAttribute("hidden", !show);
      if (show) visible += 1;
    });
    if (status) status.textContent = term ? `${visible} resultado(s) para “${term}”` : `${visible} artigo(s) visível(is)`;
    if (empty) empty.hidden = visible > 0;
  };

  const handleWelcome = () => {
    if (welcome) welcome.hidden = true;
    state.welcomeHandled = true;
    writeState(state);
  };

  page.querySelectorAll("[data-help-start], [data-help-explore]").forEach(button => {
    button.addEventListener("click", handleWelcome);
  });

  page.querySelector("[data-help-restart]")?.addEventListener("click", () => {
    checks.forEach(check => { check.checked = false; });
    state.welcomeHandled = false;
    state.checks = [];
    writeState(state);
    if (welcome) welcome.hidden = false;
    updateProgress();
    window.scrollTo({ top: 0, behavior: "smooth" });
  });

  page.querySelectorAll("[data-help-check]").forEach(check => {
    check.addEventListener("change", () => {
      state.checks = checks.filter(item => item.checked).map(item => item.value);
      writeState(state);
      updateProgress();
    });
  });

  page.querySelectorAll("[data-help-category]").forEach(button => {
    button.addEventListener("click", () => {
      selectedCategory = button.dataset.helpCategory || "*";
      page.querySelectorAll("[data-help-category]").forEach(item => item.classList.toggle("active", item === button));
      filterArticles();
    });
  });

  search?.addEventListener("input", filterArticles);
  page.querySelector("[data-help-clear]")?.addEventListener("click", () => {
    if (search instanceof HTMLInputElement) search.value = "";
    selectedCategory = "*";
    page.querySelectorAll("[data-help-category]").forEach(item => item.classList.toggle("active", item.dataset.helpCategory === "*"));
    filterArticles();
    search?.focus();
  });

  page.querySelectorAll("[data-help-related]").forEach(link => {
    link.addEventListener("click", event => {
      const id = link.dataset.helpRelated;
      const target = id ? page.querySelector(`#help-${CSS.escape(id)}`) : null;
      if (!target) return;
      event.preventDefault();
      selectedCategory = "*";
      page.querySelectorAll("[data-help-category]").forEach(item => item.classList.toggle("active", item.dataset.helpCategory === "*"));
      if (search instanceof HTMLInputElement) search.value = "";
      filterArticles();
      target.scrollIntoView({ behavior: "smooth", block: "start" });
      target.classList.add("help-article-focus");
      window.setTimeout(() => target.classList.remove("help-article-focus"), 1400);
    });
  });

  page.querySelectorAll("[data-help-feedback-action]").forEach(button => {
    button.addEventListener("click", () => {
      const feedback = button.closest("[data-help-feedback]");
      if (!feedback) return;
      feedback.textContent = button.dataset.helpFeedbackAction === "yes"
        ? "Obrigado pelo retorno."
        : "Anotado. Tente outra busca ou fale com o responsável pelo ADMIN.";
    });
  });

  updateProgress();
  filterArticles();
});
