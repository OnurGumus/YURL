<div align="center">
  <!-- TODO: Add your project logo or banner here -->
  <!-- <img src="path/to/your/logo.png" alt="Project Logo" width="150"/> -->
  <h1>✨ URL Shortener ✨</h1>
</div>

This project is a URL shortener service built with F# and the Giraffe web framework. It allows users to submit a long URL and receive a shorter, unique slug that redirects to the original URL. It leverages LLM capabilities for enhanced functionality (e.g., intelligent slug generation).

<!-- TODO: Add relevant badges here (e.g., Build Status, License, Version) -->
<!-- Example: -->
<!-- <p align="center">
  <img alt="Build Status" src="https://img.shields.io/github/actions/workflow/status/your-username/your-repo/build.yml?branch=main">
  <img alt="License" src="https://img.shields.io/github/license/your-username/your-repo">
  <img alt="Version" src="https://img.shields.io/github/v/release/your-username/your-repo">
</p> -->

## 🚀 Features

*   🔗 Shortens long URLs into unique slugs (potentially using LLM-based generation for more meaningful slugs).
*   ➡️ Redirects from slugs to the original URLs.
*   🔌 API endpoint for creating slugs (`/api/slug`).
*   🛡️ Rate limiting on the API endpoint.
*   🧠 Uses a CQRS pattern for handling data.
*   ⚙️ Configuration via HOCON files.

## 🤔 Why This Project?

*   Provide a simple and efficient URL shortening service.
*   Explore the integration of LLMs in a practical web application.
*   Demonstrate building web services with F# and Giraffe.

## 🛠️ Building and Running

(Please replace with your specific build and run commands if different)

1.  **Build the project:**
    ```bash
    # Assuming a .NET project structure
    dotnet build
    ```
2.  **Run the project:**
    ```bash
    # Assuming a .NET project structure
    dotnet run --project src/Server/Server.fsproj
    ```

The application should then be accessible at the configured host and port (e.g., `http://localhost:5000`).

## 📲 API Usage

### Create a Shortened URL

*   **POST** `/api/slug`
*   **Request Body:** JSON object with a `Url` field.
    ```json
    {
        "Url": "https://www.example.com/a/very/long/url/to/shorten"
    }
    ```
*   **Success Response (200 OK):** JSON object with the `Slug`.
    ```json
    {
        "Slug": "aB1cD2eF"
    }
    ```
    *(The actual slug format might vary based on the generation logic)*

### Redirect to Original URL

*   **GET** `/{slug}`
    *   Replace `{slug}` with the actual generated slug.
    *   The service will respond with an HTTP redirect to the original URL.

## 🤝 Contributing

Contributions are welcome! If you have ideas for improvements or find any issues.

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details. 