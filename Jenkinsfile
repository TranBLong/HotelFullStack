pipeline {
    agent any

    environment {
        IMAGE_NAME  = "hotel-fullstack"
        SERVER_HOST = "192.168.100.211"
        SERVER_USER = "long"
    }

    triggers { pollSCM('H/2 * * * *') }

    stages {
        stage('Checkout') {
            steps {
                checkout([$class: 'GitSCM',
                    branches: [[name: '*/main']],
                    userRemoteConfigs: [[
                        url: 'https://github.com/TranBLong/HotelFullStack.git',
                        credentialsId: 'github-pat'
                    ]]
                ])
            }
        }

        stage('Docker Build & Push') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'dockerhub-cred',
                    usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                    sh '''
                    docker build -t docker.io/$DOCKER_USER/$IMAGE_NAME:latest \
                                 -t docker.io/$DOCKER_USER/$IMAGE_NAME:$BUILD_NUMBER .
                    echo "$DOCKER_PASS" | docker login -u "$DOCKER_USER" --password-stdin
                    docker push docker.io/$DOCKER_USER/$IMAGE_NAME:latest
                    docker push docker.io/$DOCKER_USER/$IMAGE_NAME:$BUILD_NUMBER
                    '''
                }
            }
        }

        stage('Deploy Server') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'dockerhub-cred',
                    usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                    sshagent(credentials: ['server-ssh-key']) {
                        sh '''
                        scp -o StrictHostKeyChecking=no docker-compose.prod.yml $SERVER_USER@$SERVER_HOST:~/hotel/docker-compose.yml
                        ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_HOST "
                          cd ~/hotel && \\
                          echo '$DOCKER_PASS' | docker login -u $DOCKER_USER --password-stdin && \\
                          docker compose pull app && \\
                          docker compose up -d && \\
                          docker image prune -f
                        "
                        '''
                    }
                }
            }
        }
    }
}