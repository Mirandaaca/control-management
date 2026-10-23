plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

android {
    namespace = "com.delcampo.controlgestion"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // Cambiarlo después de instalar la app obliga a desinstalarla: Android la toma como otra app.
        applicationId = "com.delcampo.controlgestion"
        // Android 7.0. Es el mínimo de Flutter 3.44 y cubre geolocator, image_picker y flutter_secure_storage. Va escrito
        // aquí para que no cambie solo al actualizar Flutter.
        minSdk = 24
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        // La clave del ambiente de pruebas (tarea 13b). El keystore y sus contraseñas llegan por variables de entorno: en CI,
        // de los secretos de GitHub; en la PC, de la carpeta de los secretos. Nunca están en el repo. Sin ellas, el APK de
        // lanzamiento de pruebas no compila: nunca cae a la clave de debug.
        create("pruebas") {
            System.getenv("ANDROID_KEYSTORE_PATH")?.let { storeFile = file(it) }
            storePassword = System.getenv("ANDROID_KEYSTORE_PASSWORD")
            keyAlias = System.getenv("ANDROID_KEY_ALIAS")
            keyPassword = System.getenv("ANDROID_KEY_PASSWORD")
        }
    }

    // Un flavor por ambiente: la app de pruebas convive con la de desarrollo en el mismo celular, con otro id y otro nombre.
    // En la tarea 18 se suma el de producción.
    flavorDimensions += "ambiente"
    productFlavors {
        create("desarrollo") {
            dimension = "ambiente"
            manifestPlaceholders["nombreDeLaApp"] = "Control de Gestión"
            // Para `flutter run --release` en la PC.
            signingConfig = signingConfigs.getByName("debug")
        }
        create("pruebas") {
            dimension = "ambiente"
            applicationIdSuffix = ".pruebas"
            manifestPlaceholders["nombreDeLaApp"] = "Control de Gestión (pruebas)"
            signingConfig = signingConfigs.getByName("pruebas")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
