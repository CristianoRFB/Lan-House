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
